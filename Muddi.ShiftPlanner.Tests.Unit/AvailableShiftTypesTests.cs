using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Muddi.ShiftPlanner.Server.Api.Services;
using Muddi.ShiftPlanner.Server.Database.Entities;
using Muddi.ShiftPlanner.Shared;
using Xunit;

namespace Muddi.ShiftPlanner.Tests.Unit;

public class AvailableShiftTypesTests
{
	private static readonly DateTime Start = new(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

	private static (ShiftContainerEntity container, ShiftTypeEntity type) CreateContainer(int count)
	{
		var type = new ShiftTypeEntity { Id = Guid.NewGuid(), Name = "Bar", Color = "#fff" };
		var framework = new ShiftFrameworkEntity
		{
			Id = Guid.NewGuid(),
			Name = "fw",
			SecondsPerShift = 3600,
			ShiftTypeCounts = []
		};
		framework.ShiftTypeCounts.Add(new ShiftFrameworkTypeCountEntity
		{
			Id = Guid.NewGuid(), ShiftFramework = framework, ShiftType = type, Count = count
		});
		var container = new ShiftContainerEntity
		{
			Id = Guid.NewGuid(),
			Start = Start,
			TotalShifts = 1,
			Framework = framework,
			Location = new ShiftLocationEntity { Id = Guid.NewGuid(), Name = "loc" },
			Shifts = new List<ShiftEntity>()
		};
		return (container, type);
	}

	private static ShiftEntity CreateShift(ShiftContainerEntity container, ShiftTypeEntity type, bool blocked)
	{
		return new ShiftEntity
		{
			Id = Guid.NewGuid(),
			ShiftContainer = container,
			Type = type,
			Start = Start,
			End = Start.AddHours(1),
			EmployeeKeycloakId = blocked ? Mappers.NotAssignedEmployee.KeycloakId : Guid.NewGuid(),
			BlockReason = blocked ? "blocked" : null
		};
	}

	[Fact]
	public void BlockedShift_ReducesAvailableCount()
	{
		var (container, type) = CreateContainer(2);
		container.Shifts.Add(CreateShift(container, type, blocked: true));

		var result = container.GetAvailableShiftTypes(Start).Single();

		result.AvailableCount.Should().Be(1);
		result.TotalCount.Should().Be(2);
	}

	[Fact]
	public void FullyBlockedSlots_AreNotAvailable()
	{
		var (container, type) = CreateContainer(1);
		container.Shifts.Add(CreateShift(container, type, blocked: true));

		container.GetAvailableShiftTypes(Start).Single().AvailableCount.Should().Be(0);
	}

	[Fact]
	public void BlockedAndAssignedShifts_BothReduceAvailableCount()
	{
		var (container, type) = CreateContainer(3);
		container.Shifts.Add(CreateShift(container, type, blocked: true));
		container.Shifts.Add(CreateShift(container, type, blocked: false));

		container.GetAvailableShiftTypes(Start).Single().AvailableCount.Should().Be(1);
	}
}
