using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FluentAssertions;
using Muddi.ShiftPlanner.Shared.Contracts.v1.Responses;
using Xunit;

namespace Muddi.ShiftPlanner.Tests.Api;

[Collection(ApiCollection.Name)]
public sealed class PublicEndpointsTests
{
	private readonly ApiFactory _factory;

	public PublicEndpointsTests(ApiFactory factory)
	{
		_factory = factory;
	}

	/// <summary>Every public data route (everything except /public/info).</summary>
	public static TheoryData<string> PublicDataRoutes =>
	[
		"/public/locations",
		$"/public/locations/{TestData.HauptbarId}",
		"/public/locations/get-all-shifts-count",
		$"/public/locations/{TestData.HauptbarId}/get-shifts-count",
		$"/public/locations/{TestData.HauptbarId}/get-all-available-shifts-types",
		"/public/shifts/available-types?IncludeNonAvailable=true"
	];

	[Theory]
	[MemberData(nameof(PublicDataRoutes))]
	public async Task PublicDataRoutes_WhenDisabled_Return404(string route)
	{
		await _factory.SetPublicBrowsingAsync(false);
		var response = await _factory.CreateAnonymousClient().GetAsync(route);
		response.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	[Theory]
	[MemberData(nameof(PublicDataRoutes))]
	public async Task PublicDataRoutes_WhenEnabled_Return200(string route)
	{
		await _factory.SetPublicBrowsingAsync(true);
		var response = await _factory.CreateAnonymousClient().GetAsync(route);
		response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task PublicInfo_WhenDisabled_RevealsOnlyTheFlag()
	{
		await _factory.SetPublicBrowsingAsync(false);
		var info = await _factory.CreateAnonymousClient().GetAsync<GetPublicInfoResponse>("/public/info");
		info.Should().NotBeNull();
		info!.PublicBrowsingEnabled.Should().BeFalse();
		info.Settings.Should().BeNull();
		info.CurrentSeason.Should().BeNull();
	}

	[Fact]
	public async Task PublicInfo_WhenEnabled_ReturnsBrandingAndCurrentSeason()
	{
		await _factory.SetPublicBrowsingAsync(true);
		var info = await _factory.CreateAnonymousClient().GetAsync<GetPublicInfoResponse>("/public/info");
		info.Should().NotBeNull();
		info!.PublicBrowsingEnabled.Should().BeTrue();
		info.Settings!.Title.Should().Be(TestData.Title);
		info.Settings.PublicBrowsingEnabled.Should().BeTrue();
		info.CurrentSeason!.Id.Should().Be(TestData.CurrentSeasonId);
	}

	[Fact]
	public async Task PublicLocations_ExcludeAdminOnlyLocations_AndOtherSeasons()
	{
		await _factory.SetPublicBrowsingAsync(true);
		var locations = await _factory.CreateAnonymousClient().GetAsync<List<GetLocationResponse>>("/public/locations");
		locations.Should().NotBeNull();
		locations!.Select(l => l.Id).Should().BeEquivalentTo([TestData.HauptbarId]);
	}

	[Fact]
	public async Task PublicLocation_StripsAdminOnlyShiftTypesFromFramework()
	{
		await _factory.SetPublicBrowsingAsync(true);
		var location = await _factory.CreateAnonymousClient()
			.GetAsync<GetLocationResponse>($"/public/locations/{TestData.HauptbarId}");
		location.Should().NotBeNull();
		var container = location!.Containers.Should().ContainSingle().Subject;
		container.Framework.ShiftTypeCounts.Select(c => c.ShiftType.Id).Should().BeEquivalentTo([TestData.ThekeTypeId]);
	}

	[Theory]
	[InlineData("22222222-2222-2222-2222-222222222222")] // Geheim: AdminOnly location type
	[InlineData("22222222-2222-2222-2222-333333333333")] // Alt: other season
	[InlineData("99999999-9999-9999-9999-999999999999")] // unknown
	public async Task PublicLocation_OfAdminOnlyOrOtherSeason_Returns404(string id)
	{
		await _factory.SetPublicBrowsingAsync(true);
		var client = _factory.CreateAnonymousClient();
		(await client.GetAsync($"/public/locations/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
		(await client.GetAsync($"/public/locations/{id}/get-shifts-count")).StatusCode.Should().Be(HttpStatusCode.NotFound);
		(await client.GetAsync($"/public/locations/{id}/get-all-available-shifts-types")).StatusCode.Should()
			.Be(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task PublicAvailableTypes_ExcludeAdminOnlyTypes_AndCountBookedAndBlockedAsOccupied()
	{
		await _factory.SetPublicBrowsingAsync(true);
		var counts = await _factory.CreateAnonymousClient().GetAsync<List<GetShiftTypesCountResponse>>(
			$"/public/locations/{TestData.HauptbarId}/get-all-available-shifts-types");
		counts.Should().NotBeNull();
		counts!.Should().OnlyContain(c => c.Type.Id == TestData.ThekeTypeId);
		counts.Should().HaveCount(2);

		var slot1 = counts.Single(c => c.Start == TestData.Slot1Start);
		slot1.TotalCount.Should().Be(2);
		slot1.AvailableCount.Should().Be(1, "one place is booked");

		var slot2 = counts.Single(c => c.Start == TestData.Slot2Start);
		slot2.TotalCount.Should().Be(2);
		slot2.AvailableCount.Should().Be(1, "a blocked place must never be offered as free to anonymous visitors");
	}

	[Fact]
	public async Task AuthenticatedAvailableTypes_KeepExistingBehaviour_BlockedDoesNotReduceAvailability()
	{
		var counts = await _factory.CreateClientWithRoles("viewer").GetAsync<List<GetShiftTypesCountResponse>>(
			$"/locations/{TestData.HauptbarId}/get-all-available-shifts-types");
		counts.Should().NotBeNull();
		var slot2 = counts!.Single(c => c.Start == TestData.Slot2Start && c.Type.Id == TestData.ThekeTypeId);
		slot2.AvailableCount.Should().Be(2, "logged-in users see blocked places separately in the roster");
	}

	[Fact]
	public async Task PublicSeasonWideAvailableTypes_CoverOnlyVisibleLocationsOfCurrentSeason()
	{
		await _factory.SetPublicBrowsingAsync(true);
		var counts = await _factory.CreateAnonymousClient().GetAsync<List<GetShiftTypesCountResponse>>(
			"/public/shifts/available-types?IncludeNonAvailable=true");
		counts.Should().NotBeNull();
		counts!.Should().NotBeEmpty();
		counts.Select(c => c.LocationId).Distinct().Should().BeEquivalentTo([TestData.HauptbarId]);
		counts.Should().OnlyContain(c => c.Type.Id == TestData.ThekeTypeId);
	}

	[Fact]
	public async Task PublicShiftsCount_IsAggregateOfVisibleLocations()
	{
		await _factory.SetPublicBrowsingAsync(true);
		var client = _factory.CreateAnonymousClient();
		var all = await client.GetAsync<List<GetShiftsCountResponse>>("/public/locations/get-all-shifts-count");
		all.Should().NotBeNull();
		var hauptbar = all!.Should().ContainSingle().Subject;
		hauptbar.Id.Should().Be(TestData.HauptbarId);
		hauptbar.TotalShifts.Should().Be(2 * (2 + 1));
		hauptbar.AssignedShifts.Should().Be(2);

		var single = await client.GetAsync<GetShiftsCountResponse>($"/public/locations/{TestData.HauptbarId}/get-shifts-count");
		single.Should().BeEquivalentTo(hauptbar);
	}

	private static readonly Regex ForbiddenKeys = new("\"(employee[a-z]*|blockReason|email|firstName|lastName|fullName)\"",
		RegexOptions.IgnoreCase | RegexOptions.Compiled);

	[Theory]
	[MemberData(nameof(PublicDataRoutes))]
	[InlineData("/public/info")]
	public async Task PublicResponses_ContainNoPersonalData(string route)
	{
		await _factory.SetPublicBrowsingAsync(true);
		var body = await _factory.CreateAnonymousClient().GetStringAsync(route);

		body.Should().NotContainEquivalentOf(TestData.EmployeeId.ToString(), "booker ids must not leak");
		body.Should().NotContain(TestData.BlockReason, "block reasons must not leak");
		ForbiddenKeys.IsMatch(body).Should().BeFalse($"response of {route} contains a personal-data key: {body}");
	}
}
