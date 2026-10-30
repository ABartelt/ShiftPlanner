using System;
using FluentAssertions;
using Muddi.ShiftPlanner.Shared.Extensions;
using Xunit;

namespace Muddi.ShiftPlanner.Tests.Unit;

public class ShiftRulesTests
{
	private static readonly DateTime Start = new(2026, 7, 10, 8, 0, 0, DateTimeKind.Utc);

	[Fact]
	public void ShouldLockAt16TheDayBefore()
	{
		ShiftRules.ReleaseLockedFrom(Start).Should().Be(new DateTime(2026, 7, 9, 16, 0, 0, DateTimeKind.Utc));
	}

	[Fact]
	public void ShouldLockTheDayBefore_WhenShiftStartsJustAfterMidnight()
	{
		var start = new DateTime(2026, 7, 10, 0, 30, 0, DateTimeKind.Utc);
		ShiftRules.ReleaseLockedFrom(start).Should().Be(new DateTime(2026, 7, 9, 16, 0, 0, DateTimeKind.Utc));
	}

	[Theory]
	[InlineData("2026-07-08T16:00:00Z", false)]
	[InlineData("2026-07-09T15:59:59Z", false)]
	[InlineData("2026-07-09T16:00:00Z", true)]
	[InlineData("2026-07-10T07:00:00Z", true)]
	[InlineData("2026-07-10T09:00:00Z", true)]
	public void ShouldBeLocked_FromLockTimeOn(string nowIso, bool locked)
	{
		var now = DateTime.Parse(nowIso, null, System.Globalization.DateTimeStyles.AdjustToUniversal);
		ShiftRules.IsReleaseLocked(Start, now).Should().Be(locked);
	}

	[Fact]
	public void ShouldAddStartingTimeShift()
	{
		ShiftRules.EffectiveStart(Start, TimeSpan.FromMinutes(30)).Should().Be(Start.AddMinutes(30));
		ShiftRules.EffectiveStart(Start, TimeSpan.Zero).Should().Be(Start);
	}
}
