using Microsoft.EntityFrameworkCore;
using Muddi.ShiftPlanner.Server.Database.Contexts;
using Muddi.ShiftPlanner.Server.Database.Entities;
using Muddi.ShiftPlanner.Shared.Contracts.v1;
using Muddi.ShiftPlanner.Shared.Extensions;

namespace Muddi.ShiftPlanner.Services.Alerting.Notifications;

/// <summary>
/// Releases (deletes) a shift on behalf of the user a reminder was sent to, with the same time-lock as the API.
/// </summary>
public class ShiftReleaseService
{
	/// <summary>
	/// How long a "JA" after a release request counts as confirmation.
	/// </summary>
	public static readonly TimeSpan ConfirmationWindow = TimeSpan.FromMinutes(15);

	private readonly ShiftPlannerContext _db;
	private readonly ShiftFormatter _formatter;
	private readonly TimeProvider _time;
	private readonly ILogger<ShiftReleaseService> _logger;

	public ShiftReleaseService(ShiftPlannerContext db, ShiftFormatter formatter, TimeProvider time,
		ILogger<ShiftReleaseService> logger)
	{
		_db = db;
		_formatter = formatter;
		_time = time;
		_logger = logger;
	}

	private DateTime Now => _time.GetUtcNow().UtcDateTime;

	private IQueryable<ShiftNotificationEntity> Notifications => _db.ShiftNotifications
		.Include(n => n.Shift).ThenInclude(s => s!.Type)
		.Include(n => n.Shift).ThenInclude(s => s!.ShiftContainer).ThenInclude(c => c.Location)
		.Include(n => n.Shift).ThenInclude(s => s!.ShiftContainer).ThenInclude(c => c.Framework);

	private IQueryable<ShiftNotificationEntity> OpenDayBefore(NotificationChannel channel, string address)
	{
		var now = Now;
		return Notifications.Where(n => n.Channel == channel && n.Address == address
		                                                    && n.Kind == ReminderKind.DayBefore
		                                                    && n.ActionUsedAt == null
		                                                    && n.Shift != null && n.Shift.Start > now);
	}

	/// <summary>
	/// The reminder must have been sent to this address, so a forwarded button can't release someone else's shift.
	/// </summary>
	public Task<ShiftNotificationEntity?> FindAsync(Guid id, NotificationChannel channel, string address,
		CancellationToken ct)
		=> Notifications.FirstOrDefaultAsync(n => n.Id == id && n.Channel == channel && n.Address == address, ct);

	public Task<ShiftNotificationEntity?> FindByTokenAsync(string token, CancellationToken ct)
		=> Notifications.FirstOrDefaultAsync(n => n.ActionToken == token, ct);

	public Task<ShiftNotificationEntity?> FindLatestOpenAsync(NotificationChannel channel, string address,
		CancellationToken ct)
		=> OpenDayBefore(channel, address).OrderByDescending(n => n.CreatedAt).FirstOrDefaultAsync(ct);

	public Task<ShiftNotificationEntity?> FindOpenByCodeAsync(NotificationChannel channel, string address,
		string code, CancellationToken ct)
		=> OpenDayBefore(channel, address).Where(n => n.ActionCode == code)
			.OrderByDescending(n => n.CreatedAt).FirstOrDefaultAsync(ct);

	public Task<ShiftNotificationEntity?> FindAwaitingConfirmationAsync(NotificationChannel channel, string address,
		CancellationToken ct)
	{
		var since = Now - ConfirmationWindow;
		return OpenDayBefore(channel, address).Where(n => n.ReleaseRequestedAt >= since)
			.OrderByDescending(n => n.ReleaseRequestedAt).FirstOrDefaultAsync(ct);
	}

	/// <summary>
	/// What would happen on release, without changing anything.
	/// </summary>
	public async Task<ReleaseResult> CheckAsync(ShiftNotificationEntity notification, CancellationToken ct)
	{
		if (notification.ActionUsedAt is not null)
			return new(ReleaseOutcome.AlreadyReleased);
		var shift = notification.Shift;
		if (shift is null || shift.EmployeeKeycloakId != notification.EmployeeKeycloakId)
			return new(ReleaseOutcome.NotFound);
		var summary = _formatter.Summarize(shift);
		if (ShiftRules.IsReleaseLocked(shift.Start, Now))
			return new(ReleaseOutcome.Locked, summary, await AdminContactAsync(ct));
		return new(ReleaseOutcome.Released, summary);
	}

	public async Task RequestAsync(ShiftNotificationEntity notification, CancellationToken ct)
	{
		notification.ReleaseRequestedAt = Now;
		await _db.SaveChangesAsync(ct);
	}

	public async Task CancelAsync(ShiftNotificationEntity notification, CancellationToken ct)
	{
		notification.ReleaseRequestedAt = null;
		await _db.SaveChangesAsync(ct);
	}

	public async Task<ReleaseResult> ReleaseAsync(ShiftNotificationEntity notification, CancellationToken ct)
	{
		var result = await CheckAsync(notification, ct);
		if (result.Outcome != ReleaseOutcome.Released)
			return result;

		var shift = notification.Shift!;
		_db.Shifts.Remove(shift);
		notification.ActionUsedAt = Now;
		notification.ReleaseRequestedAt = null;
		await _db.SaveChangesAsync(ct);
		_logger.LogInformation("Shift {ShiftId} released by {EmployeeId} via {Channel}", shift.Id,
			notification.EmployeeKeycloakId, notification.Channel);
		return result;
	}

	private async Task<string?> AdminContactAsync(CancellationToken ct)
	{
		var settings = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync(ct);
		return string.IsNullOrWhiteSpace(settings?.Settings.Contact) ? null : settings.Settings.Contact;
	}
}
