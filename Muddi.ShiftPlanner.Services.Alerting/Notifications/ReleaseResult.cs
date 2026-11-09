namespace Muddi.ShiftPlanner.Services.Alerting.Notifications;

public enum ReleaseOutcome
{
	Released,
	AlreadyReleased,

	/// <summary>
	/// The shift was deleted or now belongs to someone else.
	/// </summary>
	NotFound,

	/// <summary>
	/// Too close to the start, only admins can remove it.
	/// </summary>
	Locked
}

public record ReleaseResult(ReleaseOutcome Outcome, ShiftSummary? Summary = null, string? AdminContact = null);
