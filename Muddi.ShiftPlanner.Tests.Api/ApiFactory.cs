using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Muddi.ShiftPlanner.Server.Api.Services;
using Muddi.ShiftPlanner.Server.Database.Contexts;
using Xunit;

namespace Muddi.ShiftPlanner.Tests.Api;

/// <summary>
/// Boots the real API against a throw-away Postgres database (see <see cref="TestDatabase"/>; migrations run at
/// startup, exactly like production), with Keycloak replaced by <see cref="TestAuthHandler"/> and
/// <see cref="FakeKeycloakService"/>.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
	private readonly TestDatabase _postgres = new();

	public async Task InitializeAsync()
	{
		await _postgres.StartAsync();
		// Touch the server so migrations run, then seed.
		_ = Server;
		await using var scope = Services.CreateAsyncScope();
		await TestData.SeedAsync(scope.ServiceProvider.GetRequiredService<ShiftPlannerContext>());
	}

	public new async Task DisposeAsync()
	{
		await base.DisposeAsync();
		await _postgres.DisposeAsync();
	}

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		builder.UseEnvironment("Testing");
		builder.UseSetting("ConnectionStrings:ShiftPlannerDb", _postgres.ConnectionString);
		builder.UseSetting("MuddiConnect:Authority", "http://localhost:1/realms/test");
		builder.UseSetting("MuddiConnect:Audience", "shift-planner");
		builder.UseSetting("MuddiConnect:ClientId", "test-client");
		builder.UseSetting("MuddiConnect:ClientSecret", "test-secret");
		builder.UseSetting("Cors:Origins:0", "http://localhost");

		builder.ConfigureTestServices(services =>
		{
			services.AddAuthentication(o =>
				{
					o.DefaultScheme = TestAuthHandler.Scheme;
					o.DefaultAuthenticateScheme = TestAuthHandler.Scheme;
					o.DefaultChallengeScheme = TestAuthHandler.Scheme;
				})
				.AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Scheme, _ => { });

			services.RemoveAll<IKeycloakService>();
			services.AddScoped<IKeycloakService, FakeKeycloakService>();
		});
	}

	/// <summary>Anonymous client (no roles header).</summary>
	public HttpClient CreateAnonymousClient() => CreateClientWithForwardedIp();

	/// <summary>Client authenticated with the given roles, e.g. "editor,viewer".</summary>
	public HttpClient CreateClientWithRoles(string roles, Guid? sub = null)
	{
		var client = CreateClientWithForwardedIp();
		client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
		if (sub is { } s)
			client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, s.ToString());
		return client;
	}

	/// <summary>
	/// The in-memory TestServer has no remote IP; FastEndpoints' throttling then answers 403 unless the client can be
	/// identified through X-Forwarded-For (which is what a reverse proxy sets in production).
	/// </summary>
	private HttpClient CreateClientWithForwardedIp()
	{
		var client = CreateClient();
		client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.10");
		return client;
	}

	public async Task SetPublicBrowsingAsync(bool enabled)
	{
		await using var scope = Services.CreateAsyncScope();
		await TestData.SetPublicBrowsingAsync(scope.ServiceProvider.GetRequiredService<ShiftPlannerContext>(), enabled);
		scope.ServiceProvider.GetRequiredService<IAppSettingsProvider>().Invalidate();
	}
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
	public const string Name = "api";
}
