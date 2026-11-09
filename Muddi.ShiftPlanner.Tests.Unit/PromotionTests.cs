using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Muddi.ShiftPlanner.Server.Api.Services.Promotion;
using Muddi.ShiftPlanner.Server.Database.Entities;
using Muddi.ShiftPlanner.Shared;
using Muddi.ShiftPlanner.Shared.Entities;
using Xunit;

namespace Muddi.ShiftPlanner.Tests.Unit;

public class PromotionTests
{
	private const string BaseUrl = "https://schicht.example.org";
	private static readonly PromotionClock Clock = new("Europe/Berlin", TimeSpan.FromHours(8));
	private static readonly DateOnly EventStart = new(2026, 6, 20);
	private static readonly DateOnly EventEnd = new(2026, 6, 28);

	private static readonly SeasonEntity Season = new() { Id = Guid.NewGuid(), Name = "Kieler Woche" };
	private static readonly ShiftTypeEntity Bar = new() { Id = Guid.NewGuid(), Name = "Bar", Season = Season };
	private static readonly ShiftTypeEntity Boss = new() { Id = Guid.NewGuid(), Name = "Chef", OnlyAssignableByAdmin = true, Season = Season };

	/// <summary>
	/// A location with one container of <paramref name="slots"/> 4-hour slots, each needing 2× Bar and 1× Chef.
	/// </summary>
	private static (ShiftLocationEntity Location, ShiftContainerEntity Container) Location(string name, DateTime startUtc,
		int slots = 2, bool adminOnly = false)
	{
		var framework = new ShiftFrameworkEntity
		{
			Id = Guid.NewGuid(),
			Name = "4h",
			SecondsPerShift = 4 * 3600,
			Season = Season,
			ShiftTypeCounts =
			[
				new() { Id = Guid.NewGuid(), ShiftType = Bar, Count = 2 },
				new() { Id = Guid.NewGuid(), ShiftType = Boss, Count = 1 }
			]
		};
		var location = new ShiftLocationEntity
		{
			Id = Guid.NewGuid(),
			Name = name,
			Season = Season,
			Type = new ShiftLocationTypeEntity { Id = Guid.NewGuid(), Name = "t", AdminOnly = adminOnly },
			Containers = []
		};
		var container = new ShiftContainerEntity
		{
			Id = Guid.NewGuid(),
			Start = startUtc,
			End = startUtc.AddHours(4 * slots),
			TotalShifts = slots,
			Framework = framework,
			Location = location,
			Shifts = []
		};
		location.Containers.Add(container);
		return (location, container);
	}

	private static ShiftEntity Shift(ShiftContainerEntity container, DateTime start, ShiftTypeEntity type,
		string? blockReason = null) => new()
	{
		Id = Guid.NewGuid(),
		ShiftContainer = container,
		Start = start,
		End = start.AddHours(4),
		Type = type,
		EmployeeKeycloakId = blockReason is null ? Guid.NewGuid() : Mappers.NotAssignedEmployee.KeycloakId,
		BlockReason = blockReason
	};

	private static PromotionDigest Build(IEnumerable<ShiftLocationEntity> locations, DateTime from, DateTime to,
		params Guid[] filter) =>
		PromotionDigestBuilder.Build(Season.Name, PromotionRunKind.Manual, locations, from, to, Clock, BaseUrl, filter);

	private static DateTime Utc(int day, int hour) => new(2026, 6, day, hour, 0, 0, DateTimeKind.Utc);

	[Fact]
	public void DigestCountsOnlyBookableOpenShifts()
	{
		// 12:00 and 16:00 UTC = 14:00 and 18:00 in Berlin (summer time)
		var (bar, container) = Location("Bar", Utc(20, 12));
		container.Shifts.Add(Shift(container, Utc(20, 12), Bar));
		container.Shifts.Add(Shift(container, Utc(20, 16), Bar, blockReason: "Reserviert"));
		var (vip, _) = Location("VIP", Utc(20, 12), adminOnly: true);

		var digest = Build([bar, vip], Utc(20, 0), Utc(21, 0));

		// Admin-only types and locations are never promoted; blocked shifts count as taken.
		digest.TotalOpen.Should().Be(2);
		var location = digest.Days.Single().Locations.Single();
		location.Name.Should().Be("Bar");
		location.Link.Should().Be($"{BaseUrl}/locations/{bar.Id}?date=2026-06-20");
		location.Slots.Select(s => (s.Start.Hour, s.Open)).Should().Equal((14, 1), (18, 1));
	}

	[Fact]
	public void DigestRespectsRangeAndLocationFilter()
	{
		var (bar, _) = Location("Bar", Utc(20, 12));
		var (gate, _) = Location("Einlass", Utc(20, 12));

		Build([bar, gate], Utc(20, 14), Utc(21, 0)).TotalOpen.Should().Be(4, "only the 16:00 slot starts in range");
		Build([bar, gate], Utc(20, 0), Utc(21, 0), gate.Id).Days.Single().Locations.Single().Name.Should().Be("Einlass");
	}

	[Fact]
	public void LateNightShiftsBelongToTheEveningBefore()
	{
		// 22:00 UTC on the 20th = 00:00 on the 21st in Berlin, before the 08:00 day start
		var (bar, _) = Location("Bar", Utc(20, 22), slots: 1);

		var digest = Build([bar], Utc(20, 0), Utc(22, 0));

		digest.Days.Single().Date.Should().Be(new DateOnly(2026, 6, 20));
	}

	[Fact]
	public void FormatsWhatsAppWithRawLinks()
	{
		var (bar, _) = Location("Bar", Utc(20, 12));
		var text = PromotionFormatter.Format(Build([bar], Utc(20, 0), Utc(21, 0)), PromotionFormat.WhatsApp);

		text.Should().Contain("*Kieler Woche, Sa 20.06.*: noch 4 Schichten sind frei!");
		text.Should().Contain("• *Bar*: 4 frei – 14:00–18:00 (2×), 18:00–22:00 (2×)");
		text.Should().Contain($"{BaseUrl}/locations/{bar.Id}?date=2026-06-20");
		text.Should().EndWith($"Jetzt eintragen: {BaseUrl}/");
	}

	[Fact]
	public void EscapesNamesForTelegramAndSlack()
	{
		var (bar, _) = Location("Bar <&> Grill", Utc(20, 12));
		var digest = Build([bar], Utc(20, 0), Utc(21, 0));

		PromotionFormatter.Format(digest, PromotionFormat.Telegram)
			.Should().Contain("<b>Bar &lt;&amp;&gt; Grill</b></a>").And.Contain("<a href=\"");
		PromotionFormatter.Format(digest, PromotionFormat.Slack)
			.Should().Contain($"<{BaseUrl}/locations/{bar.Id}?date=2026-06-20|*Bar &lt;&amp;&gt; Grill*>");
	}

	[Fact]
	public void ShortensLongDigestsBelowTheLimit()
	{
		var locations = Enumerable.Range(0, 40)
			.Select(i => Location($"Ort {i:00}", Utc(20 + i % 8, 8), slots: 3).Location)
			.ToList();
		var digest = Build(locations, Utc(20, 0), Utc(29, 0));

		var text = PromotionFormatter.Format(digest, PromotionFormat.WhatsApp);

		text.Length.Should().BeLessThanOrEqualTo(PromotionFormatter.MaxLength(PromotionFormat.WhatsApp));
		text.Should().Contain($"noch {digest.TotalOpen} Schichten sind frei");
	}

	private static PromotionSchedule Schedule() => new()
	{
		Enabled = true,
		TimeZone = "Europe/Berlin",
		DailyEnabled = true,
		DailySendTime = new TimeSpan(19, 0, 0),
		DailyLookaheadDays = 1
	};

	private static List<PromotionRun> Due(PromotionSchedule schedule, DateTime nowUtc) =>
		PromotionPlanner.DueRuns(schedule, EventStart, EventEnd, Clock, nowUtc).ToList();

	[Fact]
	public void DailyPostIsDueTheEveningBeforeEachEventDay()
	{
		// 17:30 UTC = 19:30 in Berlin, the evening before the first event day
		var run = Due(Schedule(), Utc(19, 17).AddMinutes(30)).Single();

		run.Kind.Should().Be(PromotionRunKind.Daily);
		run.RunKey.Should().Be("daily:2026-06-19");
		run.FromUtc.Should().Be(Utc(20, 6), "the event day starts at 08:00 local");
		run.ToUtc.Should().Be(Utc(21, 6));
	}

	[Fact]
	public void DailyPostIsNotDueOutsideTheEventOrAfterTheCatchUpWindow()
	{
		Due(Schedule(), Utc(18, 17).AddMinutes(30)).Should().BeEmpty("the 19th is not an event day");
		Due(Schedule(), Utc(28, 17).AddMinutes(30)).Should().BeEmpty("the event is over the next day");
		Due(Schedule(), Utc(19, 16).AddMinutes(59)).Should().BeEmpty("too early");
		Due(Schedule(), Utc(19, 19)).Should().BeEmpty("missed by more than the catch-up window");
		Due(Schedule() with { Enabled = false }, Utc(19, 17).AddMinutes(30)).Should().BeEmpty();
	}

	[Fact]
	public void RecruitingRunsWeeklyBeforeTheEvent()
	{
		var schedule = Schedule() with
		{
			DailyEnabled = false,
			RecruitingEnabled = true,
			RecruitingDay = DayOfWeek.Monday,
			RecruitingSendTime = new TimeSpan(18, 0, 0)
		};

		// 2026-06-15 is a Monday; 16:30 UTC = 18:30 local
		var run = Due(schedule, Utc(15, 16).AddMinutes(30)).Single();
		run.Kind.Should().Be(PromotionRunKind.Recruiting);
		run.ToUtc.Should().Be(Utc(29, 6), "it covers the whole event");

		Due(schedule, Utc(22, 16).AddMinutes(30)).Should().BeEmpty("the event has started");
	}

	[Fact]
	public void LastCallCoversEachHourOnceAndRespectsQuietHours()
	{
		var schedule = Schedule() with { DailyEnabled = false, LastCallEnabled = true, LastCallHoursBefore = 3 };

		var run = Due(schedule, Utc(21, 10).AddMinutes(42)).Single();
		run.Kind.Should().Be(PromotionRunKind.LastCall);
		run.RunKey.Should().Be("lastcall:2026-06-21T10");
		run.FromUtc.Should().Be(Utc(21, 13));
		run.ToUtc.Should().Be(Utc(21, 14));

		// 21:30 UTC = 23:30 local, inside the default quiet hours 22:00–08:00
		Due(schedule, Utc(21, 21).AddMinutes(30)).Should().BeEmpty();
	}

	[Fact]
	public void NextRunsListsUpcomingPosts()
	{
		var runs = PromotionPlanner.NextRuns(Schedule(), EventStart, EventEnd, Clock, Utc(10, 12), 2).ToList();

		runs.Select(r => r.SendAtUtc).Should().Equal(Utc(19, 17), Utc(20, 17));
	}
}
