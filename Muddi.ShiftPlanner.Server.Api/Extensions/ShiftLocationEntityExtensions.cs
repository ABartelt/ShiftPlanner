using System.Security.Claims;
using Muddi.ShiftPlanner.Server.Api.Services;
using Muddi.ShiftPlanner.Server.Database.Entities;

namespace Muddi.ShiftPlanner.Server.Api.Extensions;

public static class ShiftLocationEntityExtensions
{
	/// <summary>Hides locations whose type is <c>AdminOnly</c> unless the caller is an admin.</summary>
	public static IQueryable<ShiftLocationEntity> CheckAdminOnly(this IQueryable<ShiftLocationEntity> shiftLocations,
		ClaimsPrincipal user)
		=> shiftLocations.VisibleTo(ScheduleAccess.FromPrincipal(user));

	/// <summary>Hides locations whose type is <c>AdminOnly</c> unless <paramref name="access"/> is admin.</summary>
	public static IQueryable<ShiftLocationEntity> VisibleTo(this IQueryable<ShiftLocationEntity> shiftLocations,
		ScheduleAccess access)
	{
		return access.IsAdmin
			? shiftLocations
			: shiftLocations.Where(x => x.Type.AdminOnly == false);
	}

	/// <summary>Restricts to one season when <paramref name="seasonId"/> is given; otherwise no-op.</summary>
	public static IQueryable<ShiftLocationEntity> InSeason(this IQueryable<ShiftLocationEntity> shiftLocations,
		Guid? seasonId)
	{
		return seasonId is { } id
			? shiftLocations.Where(l => l.Season.Id == id)
			: shiftLocations;
	}
}
