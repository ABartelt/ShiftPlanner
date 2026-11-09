using Muddi.ShiftPlanner.Shared.Contracts.v1.Requests;
using Muddi.ShiftPlanner.Shared.Contracts.v1.Responses;
using Refit;

namespace Muddi.ShiftPlanner.Shared.Api;

/// <summary>
/// Anonymous read-only API surface (<c>/public/*</c>). Must be registered on an HttpClient WITHOUT the
/// <c>AuthorizationMessageHandler</c>, so it can be used before login. Every method here returns
/// aggregate or structural data only; none of the response types can carry personal data.
/// Method names mirror <see cref="IMuddiShiftApi"/> so client code can switch between them mechanically.
/// </summary>
public interface IMuddiPublicShiftApi
{
	/// <summary>Always reachable; reports whether public browsing is enabled plus branding and current season.</summary>
	[Get("/public/info")]
	Task<GetPublicInfoResponse> GetInfo();

	[Get("/public/locations")]
	Task<IReadOnlyCollection<GetLocationResponse>> GetAllLocations();

	[Get("/public/locations/{id}")]
	Task<GetLocationResponse> GetLocationById(Guid id);

	[Get("/public/locations/get-all-shifts-count")]
	Task<IEnumerable<GetShiftsCountResponse>> GetAllLocationShiftsCount();

	[Get("/public/locations/{locationId}/get-shifts-count")]
	Task<GetShiftsCountResponse> GetLocationShiftsCount(Guid locationId);

	[Get("/public/locations/{id}/get-all-available-shifts-types")]
	Task<IEnumerable<GetShiftTypesCountResponse>> GetAllAvailableShiftTypesFromLocationAsync(Guid id,
		GetAvailableShiftsForLocationRequest request);

	[Get("/public/shifts/available-types")]
	Task<ICollection<GetShiftTypesCountResponse>> GetAvailableShiftTypes(GetPublicAvailableShiftTypesRequest request);
}
