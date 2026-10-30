using Muddi.ShiftPlanner.Server.Api.Services;
using Muddi.ShiftPlanner.Server.Database.Contexts;

namespace Muddi.ShiftPlanner.Server.Api.Endpoints.Locations.Shifts;

public class GetAllShiftsCountEndpoint : CrudGetAllEndpoint<GetShiftsCountRequest, GetShiftsCountResponse>
{
	public GetAllShiftsCountEndpoint(ShiftPlannerContext database) : base(database)
	{
	}

	protected override void CrudConfigure()
	{
		Roles(ApiRoles.Editor, ApiRoles.Viewer);
		Get("/locations/get-all-shifts-count");
	}

	public override async Task<List<GetShiftsCountResponse>?> CrudExecuteAsync(GetShiftsCountRequest request,
		CancellationToken ct)
		=> await Database.GetShiftsCountsAsync(request.SeasonId, ScheduleAccess.FromPrincipal(User), ct);
}
