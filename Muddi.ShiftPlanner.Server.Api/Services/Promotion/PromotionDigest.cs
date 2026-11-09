using Muddi.ShiftPlanner.Server.Database.Entities;
using Muddi.ShiftPlanner.Shared.Entities;

namespace Muddi.ShiftPlanner.Server.Api.Services.Promotion;

/// <summary>
/// Open time window at one location; start and end are local times of the event.
/// </summary>
public record PromotionSlot(DateTime Start, DateTime End, int Open);

public record PromotionLocation(Guid Id, string Name, string Link, IReadOnlyList<PromotionSlot> Slots)
{
	public int Open => Slots.Sum(s => s.Open);
}

public record PromotionDay(DateOnly Date, IReadOnlyList<PromotionLocation> Locations)
{
	public int Open => Locations.Sum(l => l.Open);
}

/// <summary>
/// Open shifts of a season within a time range, grouped by event day and location.
/// </summary>
public record PromotionDigest(
	string SeasonName,
	PromotionRunKind Kind,
	IReadOnlyList<PromotionDay> Days,
	string SignUpUrl)
{
	public int TotalOpen => Days.Sum(d => d.Open);
}

/// <summary>
/// Collects the open shifts volunteers can book themselves.
/// </summary>
public static class PromotionDigestBuilder
{
	/// <param name="locations">Locations with Type, Containers.Framework.ShiftTypeCounts.ShiftType and Containers.Shifts.Type loaded.</param>
	/// <param name="fromUtc">Only slots starting at or after this instant.</param>
	/// <param name="toUtc">Only slots starting before this instant.</param>
	/// <param name="clock">Event time zone and day boundary.</param>
	/// <param name="locationFilter">Only these locations; empty means all.</param>
	public static PromotionDigest Build(
		string seasonName,
		PromotionRunKind kind,
		IEnumerable<ShiftLocationEntity> locations,
		DateTime fromUtc,
		DateTime toUtc,
		PromotionClock clock,
		string baseUrl,
		IReadOnlyCollection<Guid> locationFilter)
	{
		baseUrl = baseUrl.TrimEnd('/');
		var slots = new List<(ShiftLocationEntity Location, DateTime Start, DateTime End, int Open)>();
		foreach (var location in locations)
		{
			// Admin-only locations and shift types can't be booked by volunteers, so they are never promoted.
			if (location.Type.AdminOnly)
				continue;
			if (locationFilter.Count > 0 && !locationFilter.Contains(location.Id))
				continue;

			foreach (var container in location.Containers)
			foreach (var start in container.GetStartTimes())
			{
				var startUtc = start.ToUniversalTime();
				if (startUtc < fromUtc || startUtc >= toUtc)
					continue;

				// Blocked shifts count as taken: they hold the slot without an employee.
				var open = container.Framework.ShiftTypeCounts
					.Where(stc => !stc.ShiftType.OnlyAssignableByAdmin)
					.Sum(stc => Math.Max(0, stc.Count - container.Shifts.Count(s =>
						s.Start.ToUniversalTime() == startUtc && s.Type.Id == stc.ShiftType.Id)));
				if (open > 0)
					slots.Add((location, startUtc, startUtc + container.Framework.TimePerShift, open));
			}
		}

		var days = slots
			.GroupBy(s => clock.EventDay(s.Start))
			.OrderBy(g => g.Key)
			.Select(day => new PromotionDay(day.Key, day
				.GroupBy(s => s.Location)
				.OrderBy(g => g.Key.Name, StringComparer.CurrentCultureIgnoreCase)
				.Select(l => new PromotionLocation(
					l.Key.Id,
					l.Key.Name,
					$"{baseUrl}/locations/{l.Key.Id}?date={day.Key:yyyy-MM-dd}",
					l.GroupBy(s => (s.Start, s.End))
						.OrderBy(g => g.Key.Start)
						.Select(g => new PromotionSlot(clock.ToLocal(g.Key.Start), clock.ToLocal(g.Key.End), g.Sum(s => s.Open)))
						.ToList()))
				.ToList()))
			.ToList();

		return new PromotionDigest(seasonName, kind, days, baseUrl + "/");
	}
}
