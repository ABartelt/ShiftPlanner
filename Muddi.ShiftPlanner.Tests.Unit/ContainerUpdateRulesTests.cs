using System;
using System.Collections.Generic;
using FluentAssertions;
using Muddi.ShiftPlanner.Shared.Extensions;
using Xunit;

namespace Muddi.ShiftPlanner.Tests.Unit;

public class ContainerUpdateRulesTests
{
	private static readonly Guid Bar = Guid.NewGuid();
	private static readonly Guid Chef = Guid.NewGuid();
	private static readonly DateTime Start = new(2026, 7, 10, 8, 0, 0, DateTimeKind.Utc);
	private const int Hour = 3600;

	[Fact]
	public void AllowsAnythingWithoutShifts()
	{
		ContainerUpdateRules.Validate(Start.AddDays(3), 900, Hour, 1, new[] { Chef }, Array.Empty<(DateTime, Guid)>())
			.Should().BeNull();
	}

	[Fact]
	public void AllowsExtendingAndRecolouringWhenShiftsStillFit()
	{
		var shifts = new List<(DateTime, Guid)> { (Start, Bar), (Start.AddHours(1), Bar) };
		ContainerUpdateRules.Validate(Start, Hour, Hour, 6, new[] { Bar, Chef }, shifts).Should().BeNull();
	}

	[Fact]
	public void AllowsMovingEarlierWhenSlotsStillAlign()
	{
		var shifts = new List<(DateTime, Guid)> { (Start, Bar) };
		ContainerUpdateRules.Validate(Start.AddHours(-2), Hour, Hour, 4, new[] { Bar }, shifts).Should().BeNull();
	}

	[Fact]
	public void RejectsWhenAShiftFallsOutside()
	{
		var shifts = new List<(DateTime, Guid)> { (Start.AddHours(3), Bar) };
		ContainerUpdateRules.Validate(Start, Hour, Hour, 2, new[] { Bar }, shifts).Should().Contain("outside");
	}

	[Fact]
	public void RejectsWhenSlotsNoLongerAlign()
	{
		var shifts = new List<(DateTime, Guid)> { (Start, Bar) };
		ContainerUpdateRules.Validate(Start.AddMinutes(30), Hour, Hour, 4, new[] { Bar }, shifts).Should().Contain("outside");
	}

	[Fact]
	public void RejectsFrameworkWithoutTheShiftType()
	{
		var shifts = new List<(DateTime, Guid)> { (Start, Bar) };
		ContainerUpdateRules.Validate(Start, Hour, Hour, 4, new[] { Chef }, shifts).Should().Contain("type");
	}

	[Fact]
	public void RejectsChangingTheSlotLengthWhileShiftsExist()
	{
		var shifts = new List<(DateTime, Guid)> { (Start, Bar) };
		ContainerUpdateRules.Validate(Start, 2 * Hour, Hour, 4, new[] { Bar }, shifts).Should().Contain("time per shift");
	}
}
