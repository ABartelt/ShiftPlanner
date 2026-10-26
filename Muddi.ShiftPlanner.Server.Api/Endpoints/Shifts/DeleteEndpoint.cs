using Muddi.ShiftPlanner.Server.Database.Contexts;
using Muddi.ShiftPlanner.Shared;
using Muddi.ShiftPlanner.Shared.Contracts.v1;
using Muddi.ShiftPlanner.Shared.Extensions;

namespace Muddi.ShiftPlanner.Server.Api.Endpoints.Shifts;

public class DeleteEndpoint : CrudDeleteEndpoint
{
	public DeleteEndpoint(ShiftPlannerContext database) : base(database)
	{
	}

	protected override void CrudConfigure()
	{
		Roles(ApiRoles.Editor);
		Delete("/shifts/{Id:guid}");
	}

	protected override async Task<DeleteResponse> CrudExecuteAsync(Guid id, CancellationToken ct)
	{
		var entity = await Database.Shifts.FindAsync(new object?[] { id }, cancellationToken: ct);
		if (entity is null)
			return DeleteResponse.NotFound;

		var isAdmin = User.IsInRole(ApiRoles.Admin);

		switch (isAdmin)
		{
			case false when User.GetKeycloakId() != entity.EmployeeKeycloakId:
				await Send.ForbiddenAsync(ct);
				return DeleteResponse.Other;
			//Don't allow user to remove shifts after a certain time
			case false when ShiftRules.IsReleaseLocked(entity.Start, DateTime.UtcNow):
				await Send.LockedAsync("You are not allowed to remove the shift anymore. Ask an admin.", ct);
				return DeleteResponse.Other;
		}

		Database.Remove(entity);
		await Database.SaveChangesAsync(ct);
		return DeleteResponse.OK;
	}
}