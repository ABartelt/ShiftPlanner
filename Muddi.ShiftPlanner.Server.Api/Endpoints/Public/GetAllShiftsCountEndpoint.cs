using FastEndpoints;
using Muddi.ShiftPlanner.Server.Api.Services;
using Muddi.ShiftPlanner.Server.Database.Contexts;
using Muddi.ShiftPlanner.Server.Database.Entities;

namespace Muddi.ShiftPlanner.Server.Api.Endpoints.Public;

public sealed class GetAllShiftsCountEndpoint : PublicEndpointWithoutRequest<List<GetShiftsCountResponse>>
{
	public GetAllShiftsCountEndpoint(ShiftPlannerContext database, IAppSettingsProvider settings) : base(database, settings)
	{
	}

	protected override void PublicConfigure() => Get("/public/locations/get-all-shifts-count");

	protected override async Task<List<GetShiftsCountResponse>?> ExecutePublicAsync(EmptyRequest req, SeasonEntity season,
		CancellationToken ct)
		=> await Database.GetShiftsCountsAsync(season.Id, ScheduleAccess.Anonymous, ct);
}
