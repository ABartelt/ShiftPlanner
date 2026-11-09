using Muddi.ShiftPlanner.Shared.Entities;

namespace Muddi.ShiftPlanner.Server.Api.Services.Promotion;

/// <summary>
/// A scheduled post: which shifts (start in [FromUtc, ToUtc)) and a key that makes it run only once per channel.
/// </summary>
public record PromotionRun(PromotionRunKind Kind, string RunKey, DateTime FromUtc, DateTime ToUtc, DateTime SendAtUtc);

/// <summary>
/// Decides from a season's schedule which posts are due. Pure, so it can be unit tested.
/// </summary>
public static class PromotionPlanner
{
	/// <summary>
	/// A missed send time (e.g. during a restart) is still sent within this window, but not later.
	/// </summary>
	public static readonly TimeSpan CatchUp = TimeSpan.FromHours(2);

	public static IEnumerable<PromotionRun> DueRuns(PromotionSchedule schedule, DateOnly eventStart, DateOnly eventEnd,
		PromotionClock clock, DateTime nowUtc)
	{
		if (!schedule.Enabled)
			yield break;

		var today = DateOnly.FromDateTime(clock.ToLocal(nowUtc));
		// Today's and yesterday's send times, in case the catch-up window crosses midnight.
		foreach (var sendDay in new[] { today.AddDays(-1), today })
		{
			if (Daily(schedule, eventStart, eventEnd, clock, sendDay) is { } daily && IsDue(daily, nowUtc))
				yield return daily;
			if (Recruiting(schedule, eventStart, eventEnd, clock, sendDay) is { } recruiting && IsDue(recruiting, nowUtc))
				yield return recruiting;
		}

		if (LastCall(schedule, eventStart, eventEnd, clock, nowUtc) is { } lastCall)
			yield return lastCall;
	}

	/// <summary>
	/// Upcoming daily and recruiting posts, for the admin UI.
	/// </summary>
	public static IEnumerable<PromotionRun> NextRuns(PromotionSchedule schedule, DateOnly eventStart,
		DateOnly eventEnd, PromotionClock clock, DateTime nowUtc, int count)
	{
		var today = DateOnly.FromDateTime(clock.ToLocal(nowUtc));
		var lastDay = eventEnd < today ? today : eventEnd;
		return Enumerable.Range(0, lastDay.DayNumber - today.DayNumber + 1)
			.Select(today.AddDays)
			.SelectMany(day => new[]
			{
				Daily(schedule, eventStart, eventEnd, clock, day),
				Recruiting(schedule, eventStart, eventEnd, clock, day)
			})
			.OfType<PromotionRun>()
			.Where(r => r.SendAtUtc >= nowUtc)
			.Take(count);
	}

	private static bool IsDue(PromotionRun run, DateTime nowUtc) =>
		nowUtc >= run.SendAtUtc && nowUtc < run.SendAtUtc + CatchUp;

	/// <summary>
	/// During the event: the open shifts of the next days, posted the evening before.
	/// </summary>
	private static PromotionRun? Daily(PromotionSchedule s, DateOnly eventStart, DateOnly eventEnd,
		PromotionClock clock, DateOnly sendDay)
	{
		if (!s.DailyEnabled)
			return null;
		var first = sendDay.AddDays(1);
		var last = sendDay.AddDays(Math.Max(1, s.DailyLookaheadDays));
		if (first < eventStart)
			first = eventStart;
		if (last > eventEnd)
			last = eventEnd;
		if (first > last || last < eventStart || first > eventEnd)
			return null;
		return new PromotionRun(PromotionRunKind.Daily, $"daily:{sendDay:yyyy-MM-dd}",
			clock.StartOfDayUtc(first), clock.StartOfDayUtc(last.AddDays(1)), SendAt(clock, sendDay, s.DailySendTime));
	}

	/// <summary>
	/// Before the event: a weekly overview of everything still open.
	/// </summary>
	private static PromotionRun? Recruiting(PromotionSchedule s, DateOnly eventStart, DateOnly eventEnd,
		PromotionClock clock, DateOnly sendDay)
	{
		// The day before the event the daily post takes over.
		if (!s.RecruitingEnabled || sendDay.DayOfWeek != s.RecruitingDay || sendDay >= eventStart.AddDays(-1))
			return null;
		var sendAt = SendAt(clock, sendDay, s.RecruitingSendTime);
		return new PromotionRun(PromotionRunKind.Recruiting, $"recruiting:{sendDay:yyyy-MM-dd}",
			sendAt, clock.StartOfDayUtc(eventEnd.AddDays(1)), sendAt);
	}

	/// <summary>
	/// Shifts starting in the hour that begins <see cref="PromotionSchedule.LastCallHoursBefore"/> hours from
	/// the current hour; each shift is therefore part of exactly one last-call run.
	/// </summary>
	private static PromotionRun? LastCall(PromotionSchedule s, DateOnly eventStart, DateOnly eventEnd,
		PromotionClock clock, DateTime nowUtc)
	{
		if (!s.LastCallEnabled)
			return null;
		var local = clock.ToLocal(nowUtc);
		var today = DateOnly.FromDateTime(local);
		if (today < eventStart.AddDays(-1) || today > eventEnd.AddDays(1) || IsQuiet(s, local.TimeOfDay))
			return null;
		var hour = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, nowUtc.Hour, 0, 0, DateTimeKind.Utc);
		var from = hour.AddHours(Math.Max(1, s.LastCallHoursBefore));
		return new PromotionRun(PromotionRunKind.LastCall, $"lastcall:{hour:yyyy-MM-ddTHH}", from, from.AddHours(1), hour);
	}

	public static bool IsQuiet(PromotionSchedule s, TimeSpan timeOfDay) =>
		s.QuietFrom == s.QuietUntil
			? false
			: s.QuietFrom < s.QuietUntil
				? timeOfDay >= s.QuietFrom && timeOfDay < s.QuietUntil
				: timeOfDay >= s.QuietFrom || timeOfDay < s.QuietUntil;

	private static DateTime SendAt(PromotionClock clock, DateOnly day, TimeSpan time) =>
		clock.ToUtc(day.ToDateTime(TimeOnly.MinValue) + time);
}
