using Muddi.ShiftPlanner.Shared.Entities;

namespace Muddi.ShiftPlanner.Shared;

public static class Mappers
{
	public static NotAssignedEmployee NotAssignedEmployee { get; } = new();
	public static OccupiedEmployee OccupiedEmployee { get; } = new();

	public static ShiftLocation MapToShiftLocation(this GetLocationResponse dto, GetShiftsCountResponse getShiftsCountResponse)
	{
		var containers = dto.Containers?.Select(t =>
		{
			var shiftTypeCount =
				t.Framework.ShiftTypeCounts.ToDictionary(k => k.ShiftType.MapToShiftType(), v => v.Count);
			var framework = new ShiftFramework(t.Framework.Id, TimeSpan.FromSeconds(t.Framework.SecondsPerShift), shiftTypeCount);
			return new ShiftContainer(t.Id, framework, t.Start, t.TotalShifts, t.Color);
		});
		return new ShiftLocation(dto.Id, dto.Name, dto.Type, containers ?? Enumerable.Empty<ShiftContainer>(), 
			getShiftsCountResponse.TotalShifts, getShiftsCountResponse.AssignedShifts);
	}

	public static Shift MapToShift(this GetShiftTypesCountResponse dto)
		=> new(NotAssignedEmployee, dto.Start, dto.End, dto.Type.MapToShiftType(), dto.LocationId);

	public static Shift MapToShift(this GetShiftResponse dto)
	{
		// Blocked shifts don't have a real user
		var user = !string.IsNullOrEmpty(dto.BlockReason)
			? NotAssignedEmployee
			: new Employee(dto.EmployeeId, "", dto.EmployeeFullName);
		var shift = new Shift(dto.Id,
			user,
			dto.Start.ToUniversalTime(),
			dto.End.ToUniversalTime(),
			dto.Type.MapToShiftType(),
			dto.ContainerId,
			dto.LocationId,
			dto.BlockReason);
		return shift;
	}

	public static GetShiftResponse MapToShiftResponse(this Shift dto)
	{
		return new GetShiftResponse
		{
			Id = dto.Id,
			ContainerId = dto.ContainerId,
			EmployeeId = dto.User.KeycloakId,
			EmployeeFullName = dto.User.Name,
			Start = dto.StartTime,
			End = dto.EndTime,
			Type = dto.Type.MapToShiftTypeResponse(),
			BlockReason = dto.BlockReason
		};
	}

	public static GetShiftTypesResponse MapToShiftTypeResponse(this ShiftType shiftType)
	{
		return new GetShiftTypesResponse
		{
			Id = shiftType.Id,
			Name = shiftType.Name,
			Color = shiftType.Color,
			StartingTimeShift = shiftType.StartingTimeShift,
			OnlyAssignableByAdmin = shiftType.OnlyAssignableByAdmin,
			Description = shiftType.Description
		};
	}

	public static ShiftType MapToShiftType(this GetShiftTypesResponse dto)
		=> new ShiftType(dto.Id, dto.Name, dto.Color, dto.OnlyAssignableByAdmin, dto.StartingTimeShift, dto.Description);

	public static Shift ToLocalTime(this Shift shift)
		=> new(shift.Id,
			shift.User,
			shift.StartTime.ToUniversalTime().ToLocalTime(),
			shift.EndTime.ToUniversalTime().ToLocalTime(),
			shift.Type,
			shift.ContainerId,
			shift.LocationId);
}