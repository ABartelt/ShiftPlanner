namespace Muddi.ShiftPlanner.Shared.Contracts.v1.Requests;

/// <summary>
/// Query for <c>GET /public/shifts/available-types</c>. The season is always the current one and is
/// resolved on the server, so there is intentionally no <c>SeasonId</c>.
/// </summary>
public class GetPublicAvailableShiftTypesRequest
{
	public int? Limit { get; set; }
	public bool IncludeNonAvailable { get; set; }
	public DateTime? StartingFrom { get; set; }
}
