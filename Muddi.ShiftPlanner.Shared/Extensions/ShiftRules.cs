namespace Muddi.ShiftPlanner.Shared.Extensions;

/// <summary>
/// Time rules for a single shift. Pure so the API and the notification worker apply the same rules.
/// </summary>
public static class ShiftRules
{
	/// <summary>
	/// Non-admins can no longer release (delete) a shift from 16:00 UTC the day before it starts.
	/// </summary>
	//TODO This needs to be set via Api by an admin in the future
	public static DateTime ReleaseLockedFrom(DateTime startUtc) => startUtc.Date.AddDays(-1).AddHours(16);

	public static bool IsReleaseLocked(DateTime startUtc, DateTime nowUtc) => nowUtc >= ReleaseLockedFrom(startUtc);

	/// <summary>
	/// The time the shift really starts, as shown in the client: slot start plus the type's starting time shift.
	/// </summary>
	public static DateTime EffectiveStart(DateTime startUtc, TimeSpan startingTimeShift) => startUtc + startingTimeShift;
}
