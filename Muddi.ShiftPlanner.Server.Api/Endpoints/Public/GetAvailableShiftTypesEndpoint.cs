using FastEndpoints;
using Muddi.ShiftPlanner.Server.Api.Services;
using Muddi.ShiftPlanner.Server.Database.Contexts;
using Muddi.ShiftPlanner.Server.Database.Entities;

namespace Muddi.ShiftPlanner.Server.Api.Endpoints.Public;

public sealed class GetAvailableShiftTypesEndpoint
	: PublicEndpoint<GetPublicAvailableShiftTypesRequest, List<GetShiftTypesCountResponse>>
{
	public GetAvailableShiftTypesEndpoint(ShiftPlannerContext database, IAppSettingsProvider settings)
		: base(database, settings)
	{
	}

	protected override void PublicConfigure() => Get("/public/shifts/available-types");

	protected override async Task<List<GetShiftTypesCountResponse>?> ExecutePublicAsync(
		GetPublicAvailableShiftTypesRequest req, SeasonEntity season, CancellationToken ct)
	{
		var limit = req.Limit is > 0 ? Math.Min(req.Limit.Value, ScheduleQueries.MaxPublicLimit) : ScheduleQueries.MaxPublicLimit;
		return await Database.GetAvailableShiftTypesForSeasonAsync(season.Id, req.StartingFrom, limit,
			req.IncludeNonAvailable, ScheduleAccess.Anonymous, ct);
	}
}
