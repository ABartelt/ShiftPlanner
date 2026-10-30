namespace Muddi.ShiftPlanner.Shared.Extensions;

/// <summary>
/// Rules for changing an existing container that already has shifts. Pure so the API can validate
/// before touching the database and the rules can be unit tested.
/// </summary>
public static class ContainerUpdateRules
{
	/// <summary>
	/// Returns null when the change is allowed, otherwise a human readable reason.
	/// A shift stays valid when its start is still one of the container's slot starts and its type is
	/// still part of the framework; the slot length must not change while shifts exist.
	/// </summary>
	public static string? Validate(
		DateTime newStart,
		int newSecondsPerShift,
		int oldSecondsPerShift,
		int newTotalShifts,
		IEnumerable<Guid> frameworkTypeIds,
		IEnumerable<(DateTime Start, Guid TypeId)> shifts)
	{
		var shiftList = shifts.ToList();
		if (shiftList.Count == 0)
			return null;
		if (newSecondsPerShift != oldSecondsPerShift)
			return $"{shiftList.Count} shifts exist, the time per shift can not change while shifts are booked";

		var slotStarts = new HashSet<DateTime>();
		for (var i = 0; i < newTotalShifts; i++)
			slotStarts.Add(newStart.AddSeconds((double)newSecondsPerShift * i));
		var outside = shiftList.Count(s => !slotStarts.Contains(s.Start));
		if (outside > 0)
			return $"{outside} shifts would lie outside the container";

		var types = frameworkTypeIds.ToHashSet();
		var missing = shiftList.Count(s => !types.Contains(s.TypeId));
		if (missing > 0)
			return $"{missing} shifts have a type that is not part of the framework";

		return null;
	}
}
