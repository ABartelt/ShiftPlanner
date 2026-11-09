using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Muddi.ShiftPlanner.Client.Entities;
using Muddi.ShiftPlanner.Client.Services;
using Muddi.ShiftPlanner.Shared;
using Muddi.ShiftPlanner.Shared.Contracts.v1.Responses;
using Muddi.ShiftPlanner.Shared.Entities;
using Xunit;

namespace Muddi.ShiftPlanner.Tests.Unit;

/// <summary>The anonymous day view is built from counts only; these pin down that synthesis.</summary>
public class ShiftSynthesisTests
{
	private static readonly Guid LocationId = Guid.NewGuid();
	private static readonly DateTime Slot1 = new(2030, 6, 1, 10, 0, 0, DateTimeKind.Utc);
	private static readonly TimeSpan SlotLength = TimeSpan.FromHours(2);

	private static readonly GetShiftTypesResponse Theke = new()
		{ Id = Guid.NewGuid(), Name = "Theke", Color = "#111111", StartingTimeShift = TimeSpan.Zero };

	private static readonly GetShiftTypesResponse Kasse = new()
		{ Id = Guid.NewGuid(), Name = "Kasse", Color = "#222222", OnlyAssignableByAdmin = true };

	private static ShiftContainer Container(Guid? id = null, DateTime? start = null, int totalShifts = 2)
	{
		var framework = new ShiftFramework(Guid.NewGuid(), SlotLength,
			new Dictionary<ShiftType, int> { [Theke.MapToShiftType()] = 2, [Kasse.MapToShiftType()] = 1 });
		return new ShiftContainer(id ?? Guid.NewGuid(), framework, start ?? Slot1, totalShifts, "#fff");
	}

	private static GetShiftTypesCountResponse Count(GetShiftTypesResponse type, DateTime start, int available, int total)
		=> new() { Type = type, Start = start, End = start + SlotLength, AvailableCount = available, TotalCount = total, LocationId = LocationId };

	[Fact]
	public void EmitsOneFreeTilePerFreePlace_AndOneOccupiedTilePerTakenPlace()
	{
		var container = Container();
		var counts = new[] { Count(Theke, Slot1, 1, 2), Count(Theke, Slot1 + SlotLength, 0, 2) };

		var shifts = ShiftService.SynthesizeShiftsFromCounts(counts, [container], LocationId);

		var slot1 = shifts.Where(s => s.StartTime == Slot1).ToList();
		slot1.Count(s => s.User == Mappers.NotAssignedEmployee).Should().Be(1);
		slot1.Count(s => s.User == Mappers.OccupiedEmployee).Should().Be(1);

		var slot2 = shifts.Where(s => s.StartTime == Slot1 + SlotLength).ToList();
		slot2.Count(s => s.User == Mappers.NotAssignedEmployee).Should().Be(0);
		slot2.Count(s => s.User == Mappers.OccupiedEmployee).Should().Be(2);

		shifts.Should().OnlyContain(s => s.ContainerId == container.Id && s.LocationId == LocationId);
		shifts.Should().OnlyContain(s => s.EndTime == s.StartTime + SlotLength);
	}

	[Fact]
	public void ResolvesContainerByStartTimeAndType_WhenSeveralContainersExist()
	{
		var first = Container(start: Slot1);
		var second = Container(start: Slot1 + SlotLength * 2);
		var counts = new[] { Count(Theke, Slot1 + SlotLength * 2, 2, 2) };

		var shifts = ShiftService.SynthesizeShiftsFromCounts(counts, [first, second], LocationId);

		shifts.Should().HaveCount(2);
		shifts.Should().OnlyContain(s => s.ContainerId == second.Id);
	}

	[Fact]
	public void ClampsInconsistentCounts_AndSkipsAdminOnlyTypes()
	{
		var container = Container();
		var counts = new[]
		{
			Count(Theke, Slot1, available: 5, total: 2), // more free than total -> 2 free, 0 taken
			Count(Theke, Slot1 + SlotLength, available: -1, total: 2), // negative -> 0 free, 2 taken
			Count(Kasse, Slot1, 1, 1) // admin-only must never show up anonymously
		};

		var shifts = ShiftService.SynthesizeShiftsFromCounts(counts, [container], LocationId);

		shifts.Should().OnlyContain(s => s.Type.Name == "Theke");
		shifts.Where(s => s.StartTime == Slot1).Should().HaveCount(2).And.OnlyContain(s => s.User == Mappers.NotAssignedEmployee);
		shifts.Where(s => s.StartTime == Slot1 + SlotLength).Should().HaveCount(2).And.OnlyContain(s => s.User == Mappers.OccupiedEmployee);
	}

	[Fact]
	public void OccupiedTiles_CarryNoNameAndNoRealId()
	{
		var shift = new Shift(Mappers.OccupiedEmployee, Slot1, Slot1 + SlotLength, Theke.MapToShiftType(), LocationId);
		var appointment = new DayAppointment(shift);

		appointment.Title.Should().Be("Theke\nBesetzt");
		shift.User.KeycloakId.Should().Be(OccupiedEmployee.SentinelId).And.NotBe(Guid.Empty);
		shift.IsBlocked.Should().BeFalse();
	}

	[Fact]
	public void FreeTiles_KeepTheExistingTitle()
	{
		var shift = new Shift(Mappers.NotAssignedEmployee, Slot1, Slot1 + SlotLength, Theke.MapToShiftType(), LocationId);
		new DayAppointment(shift).Title.Should().Be("Theke\nFREI");
	}
}
