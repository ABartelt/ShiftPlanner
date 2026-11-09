using System.Security.Claims;

namespace Muddi.ShiftPlanner.Server.Api.Services;

/// <summary>
/// Explicit description of what a caller may see in the schedule. Passing this instead of a
/// <see cref="ClaimsPrincipal"/> makes the privacy boundary a visible parameter: public endpoints always
/// use <see cref="Anonymous"/>, authenticated endpoints derive it from the token.
/// </summary>
public sealed record ScheduleAccess(bool IsAuthenticated, bool IsAdmin)
{
	public static readonly ScheduleAccess Anonymous = new(false, false);

	public static ScheduleAccess FromPrincipal(ClaimsPrincipal user)
		=> new(user.Identity?.IsAuthenticated == true, user.IsInRole(ApiRoles.Admin));
}
