using FastEndpoints;
using Muddi.ShiftPlanner.Server.Api.Services;
using Muddi.ShiftPlanner.Server.Database.Contexts;
using Muddi.ShiftPlanner.Server.Database.Entities;

namespace Muddi.ShiftPlanner.Server.Api.Endpoints.Public;

public sealed class GetLocationEndpoint : PublicEndpoint<DefaultGetRequest, GetLocationResponse>
{
	public GetLocationEndpoint(ShiftPlannerContext database, IAppSettingsProvider settings) : base(database, settings)
	{
	}

	protected override void PublicConfigure() => Get("/public/locations/{Id:guid}");

	protected override Task<GetLocationResponse?> ExecutePublicAsync(DefaultGetRequest req, SeasonEntity season,
		CancellationToken ct)
		=> Database.GetLocationWithContainersAsync(req.Id, ScheduleAccess.Anonymous, ct, restrictToSeasonId: season.Id);
}
