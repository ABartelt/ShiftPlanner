using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Muddi.ShiftPlanner.Server.Database.Contexts;
using Muddi.ShiftPlanner.Server.Database.Entities;
using Muddi.ShiftPlanner.Services.Alerting.Channels;
using Muddi.ShiftPlanner.Services.Alerting.Options;
using Muddi.ShiftPlanner.Shared.Contracts.v1;
using Muddi.ShiftPlanner.Shared.Extensions;
using Npgsql;

namespace Muddi.ShiftPlanner.Services.Alerting.Notifications;

/// <summary>
/// Finds the reminders that are due and sends each one once per channel.
/// </summary>
public class ReminderDispatcher
{
	/// <summary>
	/// A row stuck in Pending (crash while sending) is tried again after this time.
	/// </summary>
	private static readonly TimeSpan PendingTimeout = TimeSpan.FromMinutes(10);

	private readonly ShiftPlannerContext _db;
	private readonly NotificationChannelRegistry _channels;
	private readonly ShiftFormatter _formatter;
	private readonly NotificationOptions _options;
	private readonly TimeProvider _time;
	private readonly ILogger<ReminderDispatcher> _logger;

	public ReminderDispatcher(ShiftPlannerContext db, NotificationChannelRegistry channels, ShiftFormatter formatter,
		IOptions<NotificationOptions> options, TimeProvider time, ILogger<ReminderDispatcher> logger)
	{
		_db = db;
		_channels = channels;
		_formatter = formatter;
		_options = options.Value;
		_time = time;
		_logger = logger;
	}

	public async Task SendDueRemindersAsync(CancellationToken ct)
	{
		if (_channels.Available.Count == 0)
			return;

		var now = _time.GetUtcNow().UtcDateTime;
		// The starting time shift of a type moves the effective start, so look a day further in both directions
		var from = now - TimeSpan.FromDays(1);
		var to = now + _options.DayBeforeLead + TimeSpan.FromDays(1);
		var shifts = await _db.Shifts.AsNoTracking()
			.Include(s => s.Type)
			.Include(s => s.ShiftContainer).ThenInclude(c => c.Location)
			.Include(s => s.ShiftContainer).ThenInclude(c => c.Framework)
			.Where(s => s.EmployeeKeycloakId != Guid.Empty && s.Start >= from && s.Start <= to)
			.AsSplitQuery()
			.ToListAsync(ct);

		var due = shifts
			.Select(s => (Shift: s, Kind: ReminderPlanner.DueKind(
				ShiftRules.EffectiveStart(s.Start, s.Type.StartingTimeShift), now,
				_options.DayBeforeLead, _options.ShortBeforeLead)))
			.Where(d => d.Kind is not null)
			.Select(d => (d.Shift, Kind: d.Kind!.Value))
			.ToList();
		if (due.Count == 0)
			return;

		var userIds = due.Select(d => d.Shift.EmployeeKeycloakId).Distinct().ToList();
		var links = (await _db.NotificationChannelLinks.AsNoTracking()
				.Where(l => l.Enabled && userIds.Contains(l.EmployeeKeycloakId))
				.ToListAsync(ct))
			.ToLookup(l => l.EmployeeKeycloakId);
		if (links.Count == 0)
			return;

		var preferences = await _db.NotificationPreferences.AsNoTracking()
			.Where(p => userIds.Contains(p.EmployeeKeycloakId))
			.ToDictionaryAsync(p => p.EmployeeKeycloakId, ct);
		var shiftIds = due.Select(d => (Guid?)d.Shift.Id).ToList();
		var existing = await _db.ShiftNotifications
			.Where(n => shiftIds.Contains(n.ShiftId))
			.ToListAsync(ct);
		var settings = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync(ct);
		var adminContact = settings?.Settings.Contact;

		foreach (var (shift, kind) in due)
		{
			if (!Wants(preferences.GetValueOrDefault(shift.EmployeeKeycloakId), kind))
				continue;

			foreach (var link in links[shift.EmployeeKeycloakId])
			{
				if (!_channels.TryGet(link.Channel, out var channel))
					continue;

				var notification = existing.FirstOrDefault(n => n.ShiftId == shift.Id
				                                                && n.EmployeeKeycloakId == shift.EmployeeKeycloakId
				                                                && n.Kind == kind
				                                                && n.ShiftStart == shift.Start
				                                                && n.Channel == link.Channel);
				if (notification is null)
				{
					notification = await TryCreateAsync(shift, kind, link, now, ct);
					if (notification is null)
						continue;
				}
				else if (!ShouldRetry(notification, now))
				{
					continue;
				}

				await SendAsync(notification, shift, link, channel, adminContact, now, ct);
			}
		}
	}

	/// <summary>
	/// Deletes old reminders and expired link codes.
	/// </summary>
	public async Task CleanupAsync(CancellationToken ct)
	{
		var now = _time.GetUtcNow().UtcDateTime;
		var cutoff = now - _options.Retention;
		var notifications = await _db.ShiftNotifications.Where(n => n.CreatedAt < cutoff).ExecuteDeleteAsync(ct);
		var codes = await _db.NotificationLinkCodes.Where(c => c.ExpiresAt < now).ExecuteDeleteAsync(ct);
		if (notifications + codes > 0)
			_logger.LogInformation("Deleted {Notifications} old reminders and {Codes} expired link codes",
				notifications, codes);
	}

	private static bool Wants(NotificationPreferencesEntity? preferences, ReminderKind kind) => kind switch
	{
		ReminderKind.DayBefore => preferences?.RemindDayBefore ?? true,
		ReminderKind.ShortBefore => preferences?.RemindShortBefore ?? true,
		_ => false
	};

	private bool ShouldRetry(ShiftNotificationEntity notification, DateTime now)
		=> notification.Attempts < _options.MaxAttempts && notification.Status switch
		{
			NotificationStatus.Failed => true,
			NotificationStatus.Pending => notification.CreatedAt < now - PendingTimeout,
			_ => false
		};

	/// <summary>
	/// Inserts the row before sending, so a second instance or tick hits the unique index instead of sending twice.
	/// </summary>
	private async Task<ShiftNotificationEntity?> TryCreateAsync(ShiftEntity shift, ReminderKind kind,
		NotificationChannelLinkEntity link, DateTime now, CancellationToken ct)
	{
		var notification = new ShiftNotificationEntity
		{
			Id = Guid.NewGuid(),
			ShiftId = shift.Id,
			EmployeeKeycloakId = shift.EmployeeKeycloakId,
			Kind = kind,
			ShiftStart = shift.Start,
			Channel = link.Channel,
			Address = link.Address,
			Status = NotificationStatus.Pending,
			CreatedAt = now,
			ActionCode = kind == ReminderKind.DayBefore ? Tokens.CreateActionCode() : null,
			ActionToken = kind == ReminderKind.DayBefore ? Tokens.Create() : null
		};
		_db.ShiftNotifications.Add(notification);
		try
		{
			await _db.SaveChangesAsync(ct);
			return notification;
		}
		catch (DbUpdateException e) when (e.InnerException is PostgresException
		                                  {
			                                  SqlState: PostgresErrorCodes.UniqueViolation
		                                  })
		{
			_db.Entry(notification).State = EntityState.Detached;
			return null;
		}
	}

	private async Task SendAsync(ShiftNotificationEntity notification, ShiftEntity shift,
		NotificationChannelLinkEntity link, INotificationChannel channel, string? adminContact, DateTime now,
		CancellationToken ct)
	{
		var canRelease = notification.Kind == ReminderKind.DayBefore && !ShiftRules.IsReleaseLocked(shift.Start, now);
		var message = _formatter.CreateReminder(notification, shift, canRelease, adminContact, link.UnsubscribeToken);
		notification.Attempts++;
		notification.Address = link.Address;
		try
		{
			await channel.SendReminderAsync(link.Address, message, ct);
			notification.Status = NotificationStatus.Sent;
			notification.SentAt = _time.GetUtcNow().UtcDateTime;
			notification.LastError = null;
			_logger.LogInformation("Sent {Kind} reminder for shift {ShiftId} via {Channel}", notification.Kind,
				shift.Id, link.Channel);
		}
		catch (Exception e) when (e is not OperationCanceledException)
		{
			notification.Status = NotificationStatus.Failed;
			notification.LastError = e.Message.Length > 500 ? e.Message[..500] : e.Message;
			_logger.LogWarning(e, "Sending {Kind} reminder for shift {ShiftId} via {Channel} failed (attempt {Attempt})",
				notification.Kind, shift.Id, link.Channel, notification.Attempts);
		}

		await _db.SaveChangesAsync(CancellationToken.None);
	}
}
