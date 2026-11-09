using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using FastEndpoints;
using FluentValidation.Results;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Muddi.ShiftPlanner.Server.Api.Endpoints;
using Muddi.ShiftPlanner.Server.Api.Endpoints.Containers;
using Muddi.ShiftPlanner.Server.Api.Exceptions;
using Muddi.ShiftPlanner.Server.Database.Contexts;
using Muddi.ShiftPlanner.Server.Database.Entities;
using Muddi.ShiftPlanner.Shared.Contracts.v1;
using Void = FastEndpoints.Void;

namespace Muddi.ShiftPlanner.Server.Api.Services;

public static class ShiftService
{
	public static IEnumerable<DateTime> GetStartTimes(this ShiftContainerEntity container)
	{
		var current = container.Start;
		for (var i = 0; i < container.TotalShifts; i++)
		{
			yield return current;
			current = current.Add(container.Framework.TimePerShift);
		}
	}

	public static GetShiftTypesCountResponse ToResponse(this ShiftFrameworkTypeCountEntity sft, DateTime startTime,
		DateTime endTime,
		Guid locationId)
	{
		return new GetShiftTypesCountResponse
		{
			Type = sft.ShiftType.Adapt<GetShiftTypesResponse>(),
			AvailableCount = sft.Count,
			TotalCount = sft.Count,
			Start = startTime,
			End = endTime,
			LocationId = locationId
		};
	}

	/// <summary>
	/// Free/total places per shift type for the slot starting at <paramref name="startTime"/>.
	/// </summary>
	/// <param name="treatBlockedAsOccupied">
	/// Blocked placeholder shifts (admin <c>BlockReason</c>) normally do not reduce the available count, because the
	/// authenticated roster shows them separately. Anonymous callers never see the roster, so for them a blocked
	/// place must count as occupied; otherwise it would be offered as free.
	/// </param>
	public static IEnumerable<GetShiftTypesCountResponse> GetAvailableShiftTypes(this ShiftContainerEntity container,
		DateTime startTime, bool treatBlockedAsOccupied = false)
	{
		startTime = startTime.ToUniversalTime();
		var endTime = startTime + container.Framework.TimePerShift;
		var counter = container.Framework.ShiftTypeCounts
			.Select(sft => sft.ToResponse(startTime, endTime, container.Location.Id))
			.ToList();
		foreach (var shift in container.Shifts.Where(s => s.Start == startTime))
		{
			var q = counter.Single(c => c.Type.Id == shift.Type.Id);
			if (!shift.IsBlocked || treatBlockedAsOccupied)
			{
				q.AvailableCount--;
			}
		}

		return counter;
	}

	public static async Task<ValidationFailure?> PreAddShiftSanityCheck(this ShiftPlannerContext db,
		ShiftContainerEntity container,
		CreateShiftRequest req,
		ClaimsPrincipal user,
		Guid ignoreUserHasShiftAtGivenTimeWhenShiftType = default)
	{
		// First, fetch existing shifts with necessary data
		var existingShifts = await db.Shifts
			.Include(s => s.Type)
			.Include(s => s.ShiftContainer).ThenInclude(c => c.Framework)
			.Include(s => s.ShiftContainer).ThenInclude(sc => sc.Location)
			.Where(entity => entity.EmployeeKeycloakId == req.EmployeeKeycloakId &&
			                 entity.Type.Id != ignoreUserHasShiftAtGivenTimeWhenShiftType)
			.AsNoTracking()
			.ToListAsync();

		// Skip blocked shifts in overlap check - blocked shifts are not assigned to any user
		var assignableShifts = existingShifts
			.Where(s => !s.IsBlocked)
			.ToList();

		// Calculate the end time of the requested shift
		var requestedShiftEnd = req.Start.Add(container.Framework.TimePerShift);

		// Now perform the overlap check in memory (after fetching data)
		var overlappingShift = assignableShifts.FirstOrDefault(entity =>
		{
			var existingShiftEnd = entity.Start.Add(entity.ShiftContainer.Framework.TimePerShift);

			// Case 1: New shift starts during an existing shift
			// e.g., Existing: 19:00-21:30, New: 20:00-21:00
			var case1 = req.Start >= entity.Start && req.Start < existingShiftEnd;

			// Case 2: New shift ends during an existing shift
			// e.g., Existing: 20:00-21:00, New: 19:00-20:30
			var case2 = requestedShiftEnd > entity.Start && requestedShiftEnd <= existingShiftEnd;

			// Case 3: New shift completely contains an existing shift
			// e.g., Existing: 20:00-21:00, New: 19:00-22:00
			var case3 = req.Start <= entity.Start && requestedShiftEnd >= existingShiftEnd;

			return case1 || case2 || case3;
		});

		if (overlappingShift is not null)
		{
			return new AlreadyShiftAtGivenTimeFailure(overlappingShift);
		}

		var availableShiftTypes = container.GetAvailableShiftTypes(req.Start);
		var shiftType = availableShiftTypes.FirstOrDefault(st => st.Type.Id == req.ShiftTypeId);
		if (shiftType is null)
			return new ValidationFailure("ShiftType", "not available");
		if (shiftType.Type.OnlyAssignableByAdmin && !user.IsInRole(ApiRoles.Admin))
			return new ValidationFailure("AdminRole", "Only available for admins");

		return null;
	}
}