using Microsoft.EntityFrameworkCore;
using Muddi.ShiftPlanner.Server.Database.Contexts;
using Muddi.ShiftPlanner.Server.Database.Entities;
using Muddi.ShiftPlanner.Services.Alerting.Notifications;
using Muddi.ShiftPlanner.Shared.Contracts.v1;

namespace Muddi.ShiftPlanner.Services.Alerting.Inbound;

/// <param name="Text">Plain text reply.</param>
/// <param name="ConfirmReleaseOf">Telegram shows Yes/No buttons for this notification.</param>
public record InboundReply(string Text, Guid? ConfirmReleaseOf = null);

/// <summary>
/// Handles messages sent to the bots, the same for every messenger.
/// </summary>
public class InboundCommandHandler
{
	private readonly ShiftPlannerContext _db;
	private readonly ShiftReleaseService _release;
	private readonly TimeProvider _time;
	private readonly ILogger<InboundCommandHandler> _logger;

	public InboundCommandHandler(ShiftPlannerContext db, ShiftReleaseService release, TimeProvider time,
		ILogger<InboundCommandHandler> logger)
	{
		_db = db;
		_release = release;
		_time = time;
		_logger = logger;
	}

	/// <param name="address">Telegram chat id or E.164 phone number of the sender.</param>
	public async Task<InboundReply> HandleAsync(NotificationChannel channel, string address, InboundCommand command,
		CancellationToken ct)
	{
		switch (command.Type)
		{
			case InboundCommandType.Link:
				return await LinkAsync(channel, address, command.Argument!, ct);
			case InboundCommandType.Stop:
				return await StopAsync(channel, address, ct);
			case InboundCommandType.ReleaseRequest:
				var requested = command.NotificationId is { } id
					? await _release.FindAsync(id, channel, address, ct)
					: await _release.FindLatestOpenAsync(channel, address, ct);
				return await RequestReleaseAsync(channel, requested, ct);
			case InboundCommandType.ReleaseConfirm:
				return await ReleaseAsync(await _release.FindAsync(command.NotificationId!.Value, channel, address, ct),
					ct);
			case InboundCommandType.ReleaseByCode:
				return await ReleaseAsync(
					await _release.FindOpenByCodeAsync(channel, address, command.Argument!, ct), ct);
			case InboundCommandType.Yes:
				return await ReleaseAsync(await _release.FindAwaitingConfirmationAsync(channel, address, ct), ct);
			case InboundCommandType.ReleaseCancel:
				return await CancelAsync(await _release.FindAsync(command.NotificationId!.Value, channel, address, ct),
					ct);
			case InboundCommandType.No:
				return await CancelAsync(await _release.FindAwaitingConfirmationAsync(channel, address, ct), ct);
			case InboundCommandType.Help:
			case InboundCommandType.Unknown:
			default:
				return new(ReminderTexts.Help(channel));
		}
	}

	private async Task<InboundReply> LinkAsync(NotificationChannel channel, string address, string code,
		CancellationToken ct)
	{
		var now = _time.GetUtcNow().UtcDateTime;
		var linkCode = await _db.NotificationLinkCodes.FirstOrDefaultAsync(c => c.Code == code, ct);
		if (linkCode is null || linkCode.Channel != channel || linkCode.ExpiresAt < now)
			return new(ReminderTexts.LinkFailed);

		var link = await _db.NotificationChannelLinks.FirstOrDefaultAsync(
			l => l.EmployeeKeycloakId == linkCode.EmployeeKeycloakId && l.Channel == channel, ct);
		if (link is null)
		{
			link = new NotificationChannelLinkEntity
			{
				Id = Guid.NewGuid(),
				EmployeeKeycloakId = linkCode.EmployeeKeycloakId,
				Channel = channel,
				Address = address,
				UnsubscribeToken = Tokens.Create()
			};
			_db.NotificationChannelLinks.Add(link);
		}

		link.Address = address;
		link.Enabled = true;
		link.LinkedAt = now;
		_db.NotificationLinkCodes.Remove(linkCode);
		await _db.SaveChangesAsync(ct);
		_logger.LogInformation("Linked {Channel} for {EmployeeId}", channel, link.EmployeeKeycloakId);
		return new(ReminderTexts.Linked);
	}

	private async Task<InboundReply> StopAsync(NotificationChannel channel, string address, CancellationToken ct)
	{
		var disabled = await _db.NotificationChannelLinks
			.Where(l => l.Channel == channel && l.Address == address && l.Enabled)
			.ExecuteUpdateAsync(u => u.SetProperty(l => l.Enabled, false), ct);
		return new(disabled > 0 ? ReminderTexts.Stopped : ReminderTexts.NotLinked);
	}

	private async Task<InboundReply> RequestReleaseAsync(NotificationChannel channel,
		ShiftNotificationEntity? notification, CancellationToken ct)
	{
		if (notification is null)
			return new(ReminderTexts.NothingToRelease);
		var check = await _release.CheckAsync(notification, ct);
		if (check.Outcome != ReleaseOutcome.Released)
			return new(ReminderTexts.ReleaseResult(check));

		await _release.RequestAsync(notification, ct);
		var withButtons = channel == NotificationChannel.Telegram;
		return new(ReminderTexts.ConfirmRelease(check.Summary!, withButtons), withButtons ? notification.Id : null);
	}

	private async Task<InboundReply> ReleaseAsync(ShiftNotificationEntity? notification, CancellationToken ct)
	{
		if (notification is null)
			return new(ReminderTexts.NothingToRelease);
		return new(ReminderTexts.ReleaseResult(await _release.ReleaseAsync(notification, ct)));
	}

	private async Task<InboundReply> CancelAsync(ShiftNotificationEntity? notification, CancellationToken ct)
	{
		if (notification is not null)
			await _release.CancelAsync(notification, ct);
		return new(ReminderTexts.Kept);
	}
}
