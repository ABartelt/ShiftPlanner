using System;
using FluentAssertions;
using Muddi.ShiftPlanner.Services.Alerting.Notifications;
using Muddi.ShiftPlanner.Shared.Contracts.v1;
using Xunit;

namespace Muddi.ShiftPlanner.Tests.Unit;

public class ReminderPlannerTests
{
	private static readonly DateTime Start = new(2026, 7, 10, 8, 0, 0, DateTimeKind.Utc);
	private static readonly TimeSpan DayBefore = TimeSpan.FromDays(1);
	private static readonly TimeSpan ShortBefore = TimeSpan.FromMinutes(30);

	private static ReminderKind? DueAt(TimeSpan beforeStart)
		=> ReminderPlanner.DueKind(Start, Start - beforeStart, DayBefore, ShortBefore);

	[Fact]
	public void ShouldSendNothing_BeforeDayBeforeWindow()
	{
		DueAt(TimeSpan.FromHours(25)).Should().BeNull();
		DueAt(TimeSpan.FromDays(1) + TimeSpan.FromSeconds(1)).Should().BeNull();
	}

	[Fact]
	public void ShouldSendDayBefore_FromLeadTimeUntilShortWindow()
	{
		DueAt(TimeSpan.FromDays(1)).Should().Be(ReminderKind.DayBefore);
		DueAt(TimeSpan.FromHours(12)).Should().Be(ReminderKind.DayBefore);
		DueAt(TimeSpan.FromMinutes(31)).Should().Be(ReminderKind.DayBefore);
	}

	[Fact]
	public void ShouldSendShortBefore_FromLeadTimeUntilStart()
	{
		DueAt(TimeSpan.FromMinutes(30)).Should().Be(ReminderKind.ShortBefore);
		DueAt(TimeSpan.FromSeconds(1)).Should().Be(ReminderKind.ShortBefore);
	}

	[Fact]
	public void ShouldSendNothing_OnceShiftStarted()
	{
		DueAt(TimeSpan.Zero).Should().BeNull();
		DueAt(TimeSpan.FromMinutes(-10)).Should().BeNull();
	}

	[Fact]
	public void ShouldSendDayBeforeLate_WhenServiceMissedTheWindowStart()
	{
		// Down from 24h before until 2h before: the day-before reminder still goes out, not skipped
		DueAt(TimeSpan.FromHours(2)).Should().Be(ReminderKind.DayBefore);
	}
}
