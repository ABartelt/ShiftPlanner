using Mapster;
using Microsoft.EntityFrameworkCore;
using Muddi.ShiftPlanner.Server.Api.Extensions;
using Muddi.ShiftPlanner.Server.Database.Contexts;
using Muddi.ShiftPlanner.Server.Database.Entities;

namespace Muddi.ShiftPlanner.Server.Api.Services;

/// <summary>
/// Schedule read queries shared by the authenticated endpoints and the anonymous <c>/public/*</c> endpoints.
/// <para>
/// PRIVACY CONTRACT: nothing in here may project <see cref="ShiftEntity.EmployeeKeycloakId"/>,
/// <see cref="ShiftEntity.BlockReason"/>, <see cref="GetShiftResponse"/> or <see cref="GetEmployeeResponse"/>,
/// and nothing here may call Keycloak. Shifts are only ever used for counting. Who booked a shift is served
/// exclusively by the authenticated roster/employee endpoints.
/// </para>
/// </summary>
public static class ScheduleQueries
{
	/// <summary>Hard cap for list sizes requested anonymously.</summary>
	public const int MaxPublicLimit = 500;

	/// <summary>The season flagged as current, falling back to the one with the latest start date.</summary>
	public static Task<SeasonEntity?> GetCurrentSeasonAsync(this ShiftPlannerContext db, CancellationToken ct)
		=> db.Seasons
			.OrderByDescending(s => s.IsSelectedAsCurrent)
			.ThenByDescending(s => s.StartDate)
			.FirstOrDefaultAsync(ct);

	/// <summary>Locations of a season (without containers), ordered by first container start then name.</summary>
	public static Task<List<GetLocationResponse>> GetLocationsAsync(this ShiftPlannerContext db, Guid seasonId,
		ScheduleAccess access, CancellationToken ct)
		=> db.ShiftLocations
			.VisibleTo(access)
			.Include(l => l.Type)
			.Where(l => l.Season.Id == seasonId)
			.OrderBy(l => l.Containers.Min(x => x.Start)) //First order by starting time
			.ThenBy(l => l.Name) //Then by name
			.Select(t => t.Adapt<GetLocationResponse>())
			.ToListAsync(ct);

	/// <summary>
	/// One location including its containers and frameworks. Returns null when it does not exist, is not visible
	/// to <paramref name="access"/>, or is outside <paramref name="restrictToSeasonId"/>.
	/// For anonymous callers, shift types that only admins may book are removed from the framework counts.
	/// </summary>
	public static async Task<GetLocationResponse?> GetLocationWithContainersAsync(this ShiftPlannerContext db, Guid id,
		ScheduleAccess access, CancellationToken ct, Guid? restrictToSeasonId = null)
	{
		var entity = await db.ShiftLocations
			.VisibleTo(access)
			.InSeason(restrictToSeasonId)
			.Include(t => t.Type)
			.Include(t => t.Containers)
			.ThenInclude(c => c.Framework)
			.ThenInclude(f => f.ShiftTypeCounts)
			.ThenInclude(c => c.ShiftType)
			.AsSingleQuery()
			.AsNoTracking()
			.FirstOrDefaultAsync(l => l.Id == id, ct);
		if (entity is null)
			return null;

		if (!access.IsAuthenticated)
		{
			foreach (var container in entity.Containers)
				container.Framework.ShiftTypeCounts.RemoveAll(stc => stc.ShiftType.OnlyAssignableByAdmin);
		}

		return entity.Adapt<GetLocationResponse>();
	}

	/// <summary>Total and assigned places per visible location of a season. Aggregates only.</summary>
	public static Task<List<GetShiftsCountResponse>> GetShiftsCountsAsync(this ShiftPlannerContext db, Guid seasonId,
		ScheduleAccess access, CancellationToken ct)
		=> db.ShiftLocations
			.VisibleTo(access)
			.Where(l => l.Season.Id == seasonId)
			.GroupBy(s => s.Id)
			.Select(l =>
				new GetShiftsCountResponse
				{
					Id = l.Key,
					TotalShifts = l.Sum(l2 => l2.Containers.Sum(c => c.TotalShifts * c.Framework.ShiftTypeCounts
						.Sum(st => st.Count))),
					AssignedShifts = l.Sum(q => q.Containers.Sum(c => c.Shifts.Count(s => s.ShiftContainer.Location.Id == l.Key)))
				}).ToListAsync(ct);

	/// <summary>Total and assigned places of one location, or null when it is not visible/not in the season.</summary>
	public static async Task<GetShiftsCountResponse?> GetShiftsCountAsync(this ShiftPlannerContext db, Guid locationId,
		ScheduleAccess access, CancellationToken ct, Guid? restrictToSeasonId = null)
	{
		var visible = db.ShiftLocations
			.VisibleTo(access)
			.InSeason(restrictToSeasonId)
			.Where(l => l.Id == locationId);
		if (!await visible.AnyAsync(ct))
			return null;

		var total = await visible
			.SumAsync(l => l.Containers
				.Sum(c => c.TotalShifts * c.Framework.ShiftTypeCounts
					.Sum(st => st.Count)), ct);
		var assigned = await db.Shifts.CountAsync(s => s.ShiftContainer.Location.Id == locationId, ct);
		return new GetShiftsCountResponse
		{
			Id = locationId,
			TotalShifts = total,
			AssignedShifts = Math.Min(assigned, total)
		};
	}

	/// <summary>
	/// Free/total places per slot and shift type for one location, ordered by start then most available first.
	/// Null when the location is not visible. Admin-only shift types are omitted for non-admins.
	/// </summary>
	public static async Task<List<GetShiftTypesCountResponse>?> GetAvailableShiftTypesForLocationAsync(
		this ShiftPlannerContext db, Guid locationId, DateTime? start, DateTime? end, int limit,
		ScheduleAccess access, CancellationToken ct, Guid? restrictToSeasonId = null)
	{
		//FastEndpoints binds DateTime as local kind, Npgsql needs UTC
		var startUtc = start?.ToUniversalTime() ?? DateTime.UnixEpoch;
		var endUtc = end?.ToUniversalTime() ?? DateTime.MaxValue;
		var location = await db.ShiftLocations
			.VisibleTo(access)
			.InSeason(restrictToSeasonId)
			.Include(l => l.Containers.Where(c => c.Start >= startUtc && c.Start < endUtc))
			.ThenInclude(c => c.Framework)
			.ThenInclude(f => f.ShiftTypeCounts)
			.ThenInclude(stc => stc.ShiftType)
			.Include(l => l.Containers.Where(c => c.Start >= startUtc && c.Start < endUtc))
			.ThenInclude(c => c.Shifts)
			.ThenInclude(s => s.Type)
			.AsSingleQuery()
			.AsNoTracking()
			.FirstOrDefaultAsync(l => l.Id == locationId, ct);
		if (location is null)
			return null;

		var treatBlockedAsOccupied = !access.IsAuthenticated;
		IEnumerable<GetShiftTypesCountResponse> shifts = location.Containers
			.SelectMany(container => container.GetStartTimes()
				.SelectMany(startTime => container.GetAvailableShiftTypes(startTime, treatBlockedAsOccupied)))
			.Where(s => access.IsAdmin || !s.Type.OnlyAssignableByAdmin)
			.OrderBy(x => x.Start)
			.ThenByDescending(x => x.AvailableCount);

		if (limit > 0)
			shifts = shifts.Take(limit);

		return shifts.ToList();
	}

	/// <summary>
	/// Free/total places per slot and shift type across all visible locations of a season, ordered by start.
	/// Admin-only shift types are omitted for non-admins.
	/// </summary>
	public static async Task<List<GetShiftTypesCountResponse>> GetAvailableShiftTypesForSeasonAsync(
		this ShiftPlannerContext db, Guid seasonId, DateTime? startingFrom, int? limit, bool includeNonAvailable,
		ScheduleAccess access, CancellationToken ct)
	{
		var locations = await db.ShiftLocations
			.VisibleTo(access)
			.Where(l => l.Season.Id == seasonId)
			.Include(l => l.Containers)
			.ThenInclude(c => c.Framework)
			.ThenInclude(f => f.ShiftTypeCounts)
			.ThenInclude(stc => stc.ShiftType)
			.Include(l => l.Containers)
			.ThenInclude(c => c.Shifts)
			.ThenInclude(s => s.Type)
			.AsSingleQuery()
			.AsNoTracking()
			.ToListAsync(ct);
		if (locations.Count == 0)
			return [];

		var startingFromUtc = startingFrom?.ToUniversalTime();
		var treatBlockedAsOccupied = !access.IsAuthenticated;

		IEnumerable<GetShiftTypesCountResponse> shifts = locations
			.SelectMany(l => l.Containers
				.SelectMany(container => container.GetStartTimes()
					.SelectMany(startTime => container.GetAvailableShiftTypes(startTime, treatBlockedAsOccupied))))
			.Where(s => (includeNonAvailable || s.AvailableCount > 0) &&
			            (access.IsAdmin || !s.Type.OnlyAssignableByAdmin))
			.OrderBy(st => st.Start);

		if (startingFromUtc is not null)
			shifts = shifts.Where(s => s.Start >= startingFromUtc);
		if (limit is > 0)
			shifts = shifts.Take(limit.Value);

		return shifts.ToList();
	}
}
