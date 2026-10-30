using Muddi.ShiftPlanner.Server.Api.Services;
using Muddi.ShiftPlanner.Server.Database.Contexts;

namespace Muddi.ShiftPlanner.Server.Api.Endpoints.Locations;

public class GetEndpoint : CrudGetEndpoint<GetLocationResponse>
{
	public GetEndpoint(ShiftPlannerContext database) : base(database)
	{
	}

	protected override void CrudConfigure()
	{
		Roles(ApiRoles.Editor, ApiRoles.Viewer);
		Get("/locations/{Id:guid}");
	}

	public override Task<GetLocationResponse?> CrudExecuteAsync(Guid id, CancellationToken ct) =>
		Database.GetLocationWithContainersAsync(id, ScheduleAccess.FromPrincipal(User), ct);
}
