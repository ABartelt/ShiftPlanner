using Muddi.ShiftPlanner.Server.Api.Services;
using Refit;

namespace Muddi.ShiftPlanner.Tests.Api;

/// <summary>Stands in for Keycloak's admin API so no identity provider is needed in tests.</summary>
public sealed class FakeKeycloakService : IKeycloakService
{
	public Task<ApiResponse<GetTokenResponse>> GetToken(GetTokenRequest tokenRequest)
		=> throw new NotSupportedException("Not needed in API tests");

	public Task<KeycloakUserRepresentation> GetUserByIdAsync(Guid reqId)
		=> Task.FromResult(new KeycloakUserRepresentation
		{
			Id = reqId,
			Email = "fake@example.org",
			Username = "fake",
			FirstName = "Fake",
			LastName = "Person",
			Enabled = true
		});

	public async Task<IEnumerable<KeycloakUserRepresentation>> GetUsers()
		=> [await GetUserByIdAsync(TestData.EmployeeId)];
}
