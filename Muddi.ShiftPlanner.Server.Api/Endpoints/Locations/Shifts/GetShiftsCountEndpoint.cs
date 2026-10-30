using Muddi.ShiftPlanner.Server.Api.Services;
using Muddi.ShiftPlanner.Server.Database.Contexts;

namespace Muddi.ShiftPlanner.Server.Api.Endpoints.Locations.Shifts;

public class GetShiftsCountEndpoint : CrudGetEndpoint<GetShiftsCountResponse>
{
	public GetShiftsCountEndpoint(ShiftPlannerContext database) : base(database)
	{
	}

	protected override void CrudConfigure()
	{
		Roles(ApiRoles.Editor, ApiRoles.Viewer);
		Get("/locations/{Id}/get-shifts-count");
	}

	public override Task<GetShiftsCountResponse?> CrudExecuteAsync(Guid id, CancellationToken ct)
		=> Database.GetShiftsCountAsync(id, ScheduleAccess.FromPrincipal(User), ct);
}
