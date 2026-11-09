namespace Muddi.ShiftPlanner.Server.Api.Services.Promotion;

/// <summary>
/// Converts between UTC and the event's local time. An event day starts at <see cref="DayStart"/>,
/// so a shift from 00:00 to 02:00 still belongs to the evening before.
/// </summary>
public class PromotionClock
{
	public TimeZoneInfo TimeZone { get; }
	public TimeSpan DayStart { get; }

	public PromotionClock(string timeZoneId, TimeSpan dayStart)
	{
		TimeZone = TryFindTimeZone(timeZoneId) ?? TimeZoneInfo.Utc;
		DayStart = dayStart < TimeSpan.Zero || dayStart > TimeSpan.FromHours(12) ? TimeSpan.Zero : dayStart;
	}

	public static TimeZoneInfo? TryFindTimeZone(string id)
	{
		try
		{
			return string.IsNullOrWhiteSpace(id) ? null : TimeZoneInfo.FindSystemTimeZoneById(id);
		}
		catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
		{
			return null;
		}
	}

	public DateTime ToLocal(DateTime utc) =>
		TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZone);

	public DateTime ToUtc(DateTime local) =>
		TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeZone);

	/// <summary>
	/// Event day of an instant.
	/// </summary>
	public DateOnly EventDay(DateTime utc) => DateOnly.FromDateTime(ToLocal(utc) - DayStart);

	/// <summary>
	/// First instant of an event day.
	/// </summary>
	public DateTime StartOfDayUtc(DateOnly day) => ToUtc(day.ToDateTime(TimeOnly.MinValue) + DayStart);

	/// <summary>
	/// Calendar date of a season boundary; the admin UI stores the plain date as UTC midnight.
	/// </summary>
	public static DateOnly SeasonDate(DateTime utc) => DateOnly.FromDateTime(utc);
}
