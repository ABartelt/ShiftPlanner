using System;
using FluentAssertions;
using Muddi.ShiftPlanner.Client.Entities;
using Xunit;

namespace Muddi.ShiftPlanner.Tests.Unit;

public class PendingShiftClaimTests
{
	private static readonly DateTime Now = new(2030, 6, 1, 8, 0, 0, DateTimeKind.Utc);
	private static readonly DateTime Start = new(2030, 6, 1, 20, 30, 0, DateTimeKind.Utc);

	private static PendingShiftClaim Sample() => PendingShiftClaim.Create(
		new Guid("22222222-2222-2222-2222-111111111111"), Guid.NewGuid(), Start, Guid.NewGuid(),
		"Hauptbar", "Theke", Now);

	[Fact]
	public void RoundTripsThroughJson_KeepingUtc()
	{
		var claim = Sample();
		var parsed = PendingShiftClaim.TryParse(claim.ToJson(), Now);

		parsed.Should().Be(claim);
		parsed!.Start.Kind.Should().Be(DateTimeKind.Utc);
		parsed.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("not json")]
	[InlineData("{}")]
	[InlineData("{\"locationId\":\"00000000-0000-0000-0000-000000000000\",\"shiftTypeId\":\"00000000-0000-0000-0000-000000000000\"}")]
	public void TryParse_ReturnsNull_ForMissingOrInvalidInput(string? json)
	{
		PendingShiftClaim.TryParse(json, Now).Should().BeNull();
	}

	[Fact]
	public void TryParse_ReturnsNull_WhenExpired()
	{
		var json = Sample().ToJson();
		PendingShiftClaim.TryParse(json, Now + PendingShiftClaim.Ttl + TimeSpan.FromMinutes(1)).Should().BeNull();
		PendingShiftClaim.TryParse(json, Now + PendingShiftClaim.Ttl - TimeSpan.FromMinutes(1)).Should().NotBeNull();
	}

	[Fact]
	public void BuildReturnPath_PointsToTheDayViewOfTheLocation()
	{
		var claim = Sample();
		claim.BuildReturnPath(new DateTime(2030, 6, 1))
			.Should().Be("/locations/22222222-2222-2222-2222-111111111111?StartDate=2030-06-01&SelectedViewIndex=0&ShowOnlyUsersShifts=False");
	}
}
