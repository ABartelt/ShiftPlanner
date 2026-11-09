using Muddi.ShiftPlanner.Shared.Contracts.v1;

namespace Muddi.ShiftPlanner.Services.Alerting.Notifications;

/// <summary>
/// Everything a channel needs to render one reminder. Times are already in the configured time zone.
/// </summary>
public record ReminderMessage
{
	public required Guid NotificationId { get; init; }
	public required ReminderKind Kind { get; init; }
	public required string ShiftTypeName { get; init; }
	public required string LocationName { get; init; }
	public required DateTime StartLocal { get; init; }
	public required DateTime EndLocal { get; init; }

	/// <summary>
	/// Location page of the shift's day in the web client.
	/// </summary>
	public required string ShiftLink { get; init; }

	/// <summary>
	/// Day-before reminder of a shift that can still be released.
	/// </summary>
	public bool CanRelease { get; init; }

	public string? ActionCode { get; init; }

	/// <summary>
	/// E-mail only: page to release the shift.
	/// </summary>
	public string? ReleaseUrl { get; init; }

	/// <summary>
	/// E-mail only: one-click unsubscribe.
	/// </summary>
	public string? UnsubscribeUrl { get; init; }

	public string? AdminContact { get; init; }
}

/// <summary>
/// Short description of a shift for replies and pages.
/// </summary>
public record ShiftSummary(string ShiftTypeName, string LocationName, DateTime StartLocal, DateTime EndLocal, string ShiftLink);
