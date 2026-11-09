using System.Net;
using FluentAssertions;
using Muddi.ShiftPlanner.Shared.Contracts.v1.Responses;
using Xunit;

namespace Muddi.ShiftPlanner.Tests.Api;

[Collection(ApiCollection.Name)]
public sealed class ProtectedEndpointsTests
{
	private readonly ApiFactory _factory;

	public ProtectedEndpointsTests(ApiFactory factory)
	{
		_factory = factory;
	}

	public static TheoryData<string> ProtectedRoutes =>
	[
		$"/locations?SeasonId={TestData.CurrentSeasonId}",
		$"/locations/{TestData.HauptbarId}",
		$"/locations/{TestData.HauptbarId}/shifts?Start=2030-01-01T00:00:00Z&End=2031-01-01T00:00:00Z",
		$"/locations/{TestData.HauptbarId}/get-all-available-shifts-types",
		$"/locations/get-all-shifts-count?SeasonId={TestData.CurrentSeasonId}",
		$"/shifts/{TestData.BookedShiftId}",
		$"/shifts?SeasonId={TestData.CurrentSeasonId}",
		$"/shifts/available-types?SeasonId={TestData.CurrentSeasonId}",
		"/employees",
		$"/employees/{TestData.EmployeeId}",
		$"/employees/{TestData.EmployeeId}/shifts?Count=5&SeasonId={TestData.CurrentSeasonId}",
		$"/employees/shifts-ics?SeasonId={TestData.CurrentSeasonId}",
		"/seasons",
		"/settings"
	];

	[Theory]
	[MemberData(nameof(ProtectedRoutes))]
	public async Task ProtectedRoutes_Anonymous_Return401_EvenWhenPublicBrowsingIsOn(string route)
	{
		await _factory.SetPublicBrowsingAsync(true);
		var response = await _factory.CreateAnonymousClient().GetAsync(route);
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Viewer_ListsLocations_WithoutAdminOnlyOnes()
	{
		var locations = await _factory.CreateClientWithRoles("editor,viewer")
			.GetAsync<List<GetLocationResponse>>($"/locations?SeasonId={TestData.CurrentSeasonId}");
		locations!.Select(l => l.Id).Should().BeEquivalentTo([TestData.HauptbarId]);
	}

	[Fact]
	public async Task Admin_SeesAdminOnlyLocations_AndAdminOnlyTypes()
	{
		var client = _factory.CreateClientWithRoles("admin,editor,viewer");
		var locations = await client.GetAsync<List<GetLocationResponse>>($"/locations?SeasonId={TestData.CurrentSeasonId}");
		locations!.Select(l => l.Id).Should().BeEquivalentTo([TestData.HauptbarId, TestData.GeheimId]);

		var location = await client.GetAsync<GetLocationResponse>($"/locations/{TestData.HauptbarId}");
		location!.Containers.Single().Framework.ShiftTypeCounts.Select(c => c.ShiftType.Id)
			.Should().BeEquivalentTo([TestData.ThekeTypeId, TestData.KasseTypeId]);
	}

	[Fact]
	public async Task Viewer_LocationDetails_KeepAdminOnlyTypesInFramework()
	{
		// Logged-in members still see admin-only slots as "FREI" tiles (with an explanation when clicked).
		var location = await _factory.CreateClientWithRoles("viewer")
			.GetAsync<GetLocationResponse>($"/locations/{TestData.HauptbarId}");
		location!.Containers.Single().Framework.ShiftTypeCounts.Select(c => c.ShiftType.Id)
			.Should().BeEquivalentTo([TestData.ThekeTypeId, TestData.KasseTypeId]);
	}

	[Fact]
	public async Task Viewer_SeasonWideAvailableTypes_ExcludeAdminOnlyLocations()
	{
		var counts = await _factory.CreateClientWithRoles("viewer").GetAsync<List<GetShiftTypesCountResponse>>(
			$"/shifts/available-types?SeasonId={TestData.CurrentSeasonId}&IncludeNonAvailable=true");
		counts!.Should().NotBeEmpty();
		counts.Select(c => c.LocationId).Distinct().Should().BeEquivalentTo([TestData.HauptbarId]);
		counts.Should().OnlyContain(c => c.Type.Id == TestData.ThekeTypeId);
	}

	[Fact]
	public async Task Viewer_ShiftsCountOfInvisibleLocation_Returns404()
	{
		var response = await _factory.CreateClientWithRoles("viewer")
			.GetAsync($"/locations/{TestData.GeheimId}/get-shifts-count");
		response.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}
}
