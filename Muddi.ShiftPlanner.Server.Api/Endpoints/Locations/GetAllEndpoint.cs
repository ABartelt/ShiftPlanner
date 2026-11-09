using Muddi.ShiftPlanner.Server.Api.Services;
using Muddi.ShiftPlanner.Server.Database.Contexts;

namespace Muddi.ShiftPlanner.Server.Api.Endpoints.Locations;

public class GetAllEndpoint : CrudGetAllEndpoint<GetLocationRequest, GetLocationResponse>
{
	public GetAllEndpoint(ShiftPlannerContext database) : base(database)
	{
	}

	protected override void CrudConfigure()
	{
		Roles(ApiRoles.Editor, ApiRoles.Viewer);
		Get("/locations");
	}

	public override async Task<List<GetLocationResponse>?> CrudExecuteAsync(GetLocationRequest req,
		CancellationToken ct) =>
		await Database.GetLocationsAsync(req.SeasonId, ScheduleAccess.FromPrincipal(User), ct);
}
