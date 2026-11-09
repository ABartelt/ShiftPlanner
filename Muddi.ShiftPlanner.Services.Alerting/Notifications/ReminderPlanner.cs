using Muddi.ShiftPlanner.Shared.Contracts.v1;

namespace Muddi.ShiftPlanner.Services.Alerting.Notifications;

public static class ReminderPlanner
{
	/// <summary>
	/// Which reminder is due now. The windows don't overlap and end at the shift start, so a reminder missed
	/// while the service was down is sent late but never after the shift started.
	/// </summary>
	public static ReminderKind? DueKind(DateTime effectiveStartUtc, DateTime nowUtc, TimeSpan dayBeforeLead,
		TimeSpan shortBeforeLead)
	{
		if (nowUtc >= effectiveStartUtc)
			return null;
		if (nowUtc >= effectiveStartUtc - shortBeforeLead)
			return ReminderKind.ShortBefore;
		if (nowUtc >= effectiveStartUtc - dayBeforeLead)
			return ReminderKind.DayBefore;
		return null;
	}
}
