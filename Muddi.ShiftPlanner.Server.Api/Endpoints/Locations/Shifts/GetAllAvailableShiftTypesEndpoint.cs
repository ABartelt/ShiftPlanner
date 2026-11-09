using Muddi.ShiftPlanner.Server.Api.Services;
using Muddi.ShiftPlanner.Server.Database.Contexts;

namespace Muddi.ShiftPlanner.Server.Api.Endpoints.Locations.Shifts;

public class
	GetAllAvailableShiftTypesEndpoint : CrudGetAllEndpoint<GetAvailableShiftsForLocationRequest,
	GetShiftTypesCountResponse>
{
	public GetAllAvailableShiftTypesEndpoint(ShiftPlannerContext database) : base(database)
	{
	}

	protected override void CrudConfigure()
	{
		Roles(ApiRoles.Editor, ApiRoles.Viewer);
		Get("/locations/{LocationId}/get-all-available-shifts-types");
	}

	public override async Task<List<GetShiftTypesCountResponse>?> CrudExecuteAsync(
		GetAvailableShiftsForLocationRequest request, CancellationToken ct)
	{
		var shifts = await Database.GetAvailableShiftTypesForLocationAsync(request.LocationId, request.StartTime,
			request.EndTime, request.Limit, ScheduleAccess.FromPrincipal(User), ct);
		if (shifts is null)
		{
			await Send.NotFoundAsync("location", ct);
			return null;
		}

		return shifts;
	}
}
