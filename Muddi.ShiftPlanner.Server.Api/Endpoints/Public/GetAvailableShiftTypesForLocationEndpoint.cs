using FastEndpoints;
using Muddi.ShiftPlanner.Server.Api.Services;
using Muddi.ShiftPlanner.Server.Database.Contexts;
using Muddi.ShiftPlanner.Server.Database.Entities;

namespace Muddi.ShiftPlanner.Server.Api.Endpoints.Public;

public sealed class GetAvailableShiftTypesForLocationEndpoint
	: PublicEndpoint<GetAvailableShiftsForLocationRequest, List<GetShiftTypesCountResponse>>
{
	public GetAvailableShiftTypesForLocationEndpoint(ShiftPlannerContext database, IAppSettingsProvider settings)
		: base(database, settings)
	{
	}

	protected override void PublicConfigure()
		=> Get("/public/locations/{LocationId:guid}/get-all-available-shifts-types");

	protected override Task<List<GetShiftTypesCountResponse>?> ExecutePublicAsync(
		GetAvailableShiftsForLocationRequest req, SeasonEntity season, CancellationToken ct)
	{
		var limit = req.Limit > 0 ? Math.Min(req.Limit, ScheduleQueries.MaxPublicLimit) : ScheduleQueries.MaxPublicLimit;
		return Database.GetAvailableShiftTypesForLocationAsync(req.LocationId, req.StartTime, req.EndTime, limit,
			ScheduleAccess.Anonymous, ct, restrictToSeasonId: season.Id);
	}
}
