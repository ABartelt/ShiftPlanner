using Microsoft.EntityFrameworkCore;
using Muddi.ShiftPlanner.Server.Api.Extensions;
using Muddi.ShiftPlanner.Server.Database.Contexts;
using Muddi.ShiftPlanner.Shared.Contracts.v1;
using Muddi.ShiftPlanner.Shared.Extensions;

namespace Muddi.ShiftPlanner.Server.Api.Endpoints.Containers;

/// <summary>
/// Changes framework, start, length and colour of a container. Colour is always allowed; the other
/// fields only when every booked shift still fits (see <see cref="ContainerUpdateRules"/>).
/// </summary>
public class UpdateEndpoint : CrudUpdateEndpoint<UpdateContainerRequest>
{
	public UpdateEndpoint(ShiftPlannerContext database) : base(database)
	{
	}

	protected override void CrudConfigure()
	{
		Roles(ApiRoles.Admin);
		Put("/containers/{Id}");
		Options(t => t.Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict));
	}

	public override async Task CrudExecuteAsync(UpdateContainerRequest request, CancellationToken ct)
	{
		var entity = await Database.Containers
			.Include(c => c.Shifts).ThenInclude(s => s.Type)
			.Include(c => c.Framework).ThenInclude(f => f.ShiftTypeCounts).ThenInclude(t => t.ShiftType)
			.Include(c => c.Location).ThenInclude(l => l.Containers)
			.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken: ct);
		if (entity is null)
		{
			await Send.NotFoundAsync("container", ct);
			return;
		}

		var framework = entity.Framework;
		if (framework.Id != request.FrameworkId)
		{
			framework = await Database.ShiftFrameworks
				.Include(f => f.ShiftTypeCounts).ThenInclude(t => t.ShiftType)
				.FirstOrDefaultAsync(f => f.Id == request.FrameworkId, cancellationToken: ct);
			if (framework is null)
			{
				await Send.NotFoundAsync(nameof(request.FrameworkId), ct);
				return;
			}
		}

		if (request.TotalShifts < 1)
		{
			await Send.StringAsync("TotalShifts must be at least 1", StatusCodes.Status400BadRequest, cancellation: ct);
			return;
		}

		if (entity.Location.Containers.Any(c => c.Id != entity.Id && c.Start == request.Start))
		{
			await Send.ConflictAsync("A container with the same starting time already exist", ct);
			return;
		}

		var error = ContainerUpdateRules.Validate(
			request.Start,
			framework.SecondsPerShift,
			entity.Framework.SecondsPerShift,
			request.TotalShifts,
			framework.ShiftTypeCounts.Select(t => t.ShiftType.Id),
			entity.Shifts.Select(s => (s.Start, s.Type.Id)));
		if (error is not null)
		{
			await Send.ConflictAsync(error, ct);
			return;
		}

		entity.Framework = framework;
		entity.Start = request.Start;
		entity.TotalShifts = request.TotalShifts;
		entity.End = request.Start + framework.TimePerShift * request.TotalShifts;
		entity.Color = request.Color;
		await Database.SaveChangesAsync(ct);
	}
}
