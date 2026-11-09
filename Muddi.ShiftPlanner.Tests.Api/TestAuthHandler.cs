using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Muddi.ShiftPlanner.Tests.Api;

/// <summary>
/// Replaces the Keycloak JWT bearer scheme in tests. A request without <see cref="RolesHeader"/> is anonymous.
/// With it, the caller gets the listed roles (comma separated) and the optional <see cref="SubHeader"/> as "sub".
/// Claim types mirror the production token (<c>roles</c>, <c>preferred_username</c>) so role checks behave the same.
/// </summary>
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
	public const string Scheme = "Test";
	public const string RolesHeader = "X-Test-Roles";
	public const string SubHeader = "X-Test-Sub";

	public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
		UrlEncoder encoder) : base(options, logger, encoder)
	{
	}

	protected override Task<AuthenticateResult> HandleAuthenticateAsync()
	{
		if (!Request.Headers.TryGetValue(RolesHeader, out var rolesHeader))
			return Task.FromResult(AuthenticateResult.NoResult());

		var sub = Request.Headers.TryGetValue(SubHeader, out var subHeader)
			? subHeader.ToString()
			: Guid.NewGuid().ToString();

		var claims = new List<Claim>
		{
			new("sub", sub),
			new("preferred_username", "test-user"),
			new("given_name", "Test"),
			new("email", "test-user@example.org")
		};
		claims.AddRange(rolesHeader.ToString()
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Select(r => new Claim("roles", r)));

		var identity = new ClaimsIdentity(claims, Scheme, nameType: "preferred_username", roleType: "roles");
		var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme);
		return Task.FromResult(AuthenticateResult.Success(ticket));
	}
}
