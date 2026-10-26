using Muddi.ShiftPlanner.Shared.Contracts.v1.Responses.Seasons;
using Muddi.ShiftPlanner.Shared.Entities;

namespace Muddi.ShiftPlanner.Shared.Contracts.v1.Responses;

/// <summary>
/// Answer of the always-reachable <c>GET /public/info</c>. Tells an anonymous client whether public
/// browsing is enabled and, only if so, the branding settings and the current season.
/// </summary>
public class GetPublicInfoResponse
{
	public bool PublicBrowsingEnabled { get; init; }

	/// <summary>Null when public browsing is disabled.</summary>
	public ApplicationSettings? Settings { get; init; }

	/// <summary>Null when public browsing is disabled or no season exists.</summary>
	public GetSeasonResponse? CurrentSeason { get; init; }
}
