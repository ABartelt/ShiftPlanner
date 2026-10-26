using FastEndpoints;
using Muddi.ShiftPlanner.Server.Api.Services;
using Muddi.ShiftPlanner.Server.Database.Contexts;
using Muddi.ShiftPlanner.Server.Database.Entities;

namespace Muddi.ShiftPlanner.Server.Api.Endpoints.Public;

public sealed class GetShiftsCountEndpoint : PublicEndpoint<DefaultGetRequest, GetShiftsCountResponse>
{
	public GetShiftsCountEndpoint(ShiftPlannerContext database, IAppSettingsProvider settings) : base(database, settings)
	{
	}

	protected override void PublicConfigure() => Get("/public/locations/{Id:guid}/get-shifts-count");

	protected override Task<GetShiftsCountResponse?> ExecutePublicAsync(DefaultGetRequest req, SeasonEntity season,
		CancellationToken ct)
		=> Database.GetShiftsCountAsync(req.Id, ScheduleAccess.Anonymous, ct, restrictToSeasonId: season.Id);
}
