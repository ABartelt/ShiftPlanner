using FastEndpoints;
using Muddi.ShiftPlanner.Server.Api.Services;
using Muddi.ShiftPlanner.Server.Database.Contexts;
using Muddi.ShiftPlanner.Server.Database.Entities;

namespace Muddi.ShiftPlanner.Server.Api.Endpoints.Public;

public sealed class GetAllLocationsEndpoint : PublicEndpointWithoutRequest<List<GetLocationResponse>>
{
	public GetAllLocationsEndpoint(ShiftPlannerContext database, IAppSettingsProvider settings) : base(database, settings)
	{
	}

	protected override void PublicConfigure() => Get("/public/locations");

	protected override async Task<List<GetLocationResponse>?> ExecutePublicAsync(EmptyRequest req, SeasonEntity season,
		CancellationToken ct)
		=> await Database.GetLocationsAsync(season.Id, ScheduleAccess.Anonymous, ct);
}
