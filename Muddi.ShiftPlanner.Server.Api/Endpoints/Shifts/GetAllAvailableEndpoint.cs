using Muddi.ShiftPlanner.Server.Api.Services;
using Muddi.ShiftPlanner.Server.Database.Contexts;

namespace Muddi.ShiftPlanner.Server.Api.Endpoints.Shifts;

public class GetAllAvailableEndpoint : CrudGetAllEndpoint<GetShiftTypesCountRequest, GetShiftTypesCountResponse>
{
	protected override void CrudConfigure()
	{
		Roles(ApiRoles.Editor, ApiRoles.Viewer);
		Get("/shifts/available-types");
	}

	public override async Task<List<GetShiftTypesCountResponse>?> CrudExecuteAsync(GetShiftTypesCountRequest request,
		CancellationToken ct)
		=> await Database.GetAvailableShiftTypesForSeasonAsync(request.SeasonId, request.StartingFrom, request.Limit,
			request.IncludeNonAvailable, ScheduleAccess.FromPrincipal(User), ct);

	public GetAllAvailableEndpoint(ShiftPlannerContext database) : base(database)
	{
	}
}
