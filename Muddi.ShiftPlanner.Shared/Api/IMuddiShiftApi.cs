using System.Reflection;
using System.Reflection.Metadata;
using Muddi.ShiftPlanner.Server.Api.Endpoints.Seasons;
using Muddi.ShiftPlanner.Shared.Contracts.v1;
using Muddi.ShiftPlanner.Shared.Contracts.v1.Requests;
using Muddi.ShiftPlanner.Shared.Contracts.v1.Responses;
using Muddi.ShiftPlanner.Shared.Contracts.v1.Responses.Seasons;
using Muddi.ShiftPlanner.Shared.Entities;
using Refit;

namespace Muddi.ShiftPlanner.Shared.Api;

public interface IMuddiShiftApi
{
	[Post("/login")]
	Task<LoginResponse> Login(LoginRequest request);

	#region Locations

	[Get("/locations/{Id}")]
	[Headers("Authorization: Bearer")]
	Task<GetLocationResponse> GetLocationById(Guid id);

	[Get("/locations")]
	[Headers("Authorization: Bearer")]
	Task<IReadOnlyCollection<GetLocationResponse>> GetAllLocations(GetLocationRequest request);

	[Post("/locations")]
	[Headers("Authorization: Bearer")]
	Task CreateLocation(CreateLocationRequest createLocationRequest);

	[Delete("/locations/{Id}")]
	[Headers("Authorization: Bearer")]
	Task DeleteLocationAsync(Guid id);

	[Get("/locations/{Id}/export/xlsx")]
	Task<HttpContent> LocationExportToExcel(Guid Id, ExportToExcelRequest request);

	#endregion

	#region location-types

	[Get("/location-types")]
	[Headers("Authorization: Bearer")]
	Task<IReadOnlyCollection<GetLocationTypesResponse>> GetAllLocationTypes();

	[Post("/location-types")]
	[Headers("Authorization: Bearer")]
	Task CreateLocationType(CreateLocationTypeRequest createLocationTypeRequest);

	#endregion

	#region shift-types

	[Get("/shift-types")]
	[Headers("Authorization: Bearer")]
	Task<IReadOnlyCollection<GetShiftTypesResponse>> GetAllShiftTypes(GetShiftTypesRequest request);

	[Post("/shift-types")]
	[Headers("Authorization: Bearer")]
	Task CreateShiftType(CreateShiftTypeRequest createShiftTypeRequest);

	[Delete("/shift-types/{Id}")]
	[Headers("Authorization: Bearer")]
	Task DeleteShiftType(Guid id);

	[Put("/shift-types/{EntityToEdit.Id}")]
	[Headers("Authorization: Bearer")]
	Task UpdateShiftType(GetShiftTypesResponse entityToEdit);

	#endregion

	[Get("/frameworks")]
	[Headers("Authorization: Bearer")]
	Task<IReadOnlyCollection<GetFrameworkResponse>> GetAllFrameworks(GetFrameworkRequest request);

	[Delete("/frameworks/{Id}")]
	[Headers("Authorization: Bearer")]
	Task DeleteFramework(Guid id);

	[Post("/frameworks")]
	[Headers("Authorization: Bearer")]
	Task CreateFramework(CreateFrameworkRequest createFrameworkRequest);

	[Put("/frameworks/{id}")]
	[Headers("Authorization: Bearer")]
	Task UpdateFramework(Guid id, UpdateFrameworkRequest entityToEdit);

	[Post("/containers")]
	[Headers("Authorization: Bearer")]
	Task CreateContainer(CreateContainerRequest createContainerRequest);

	[Put("/containers/{id}")]
	Task UpdateContainer(Guid id, [Body] UpdateContainerRequest request);

	[Delete("/containers/{id}")]
	[Headers("Authorization: Bearer")]
	Task DeleteContainer(Guid id);


	[Get("/locations/{Id}/shifts")]
	[Headers("Authorization: Bearer")]
	Task<IEnumerable<GetShiftResponse>> GetAllShiftsForLocation(Guid id, GetAllShiftsForLocationRequest request);

	[Post("/locations/{Id}/shifts")]
	[Headers("Authorization: Bearer")]
	Task<IApiResponse> CreateShiftForLocation(Guid id, CreateShiftRequest createShiftRequest);

	[Get("/containers/{containerId}/get-available-shift-types")]
	[Headers("Authorization: Bearer")]
	Task<IEnumerable<GetShiftTypesCountResponse>> GetAvailableShiftTypes(Guid containerId, DateTime start);

	[Post("/containers/{Id}/shifts")]
	[Headers("Authorization: Bearer")]
	Task<DefaultCreateResponse> CreateShiftForContainer(Guid id, CreateShiftRequest createShiftRequest);

	[Delete("/shifts/{id}")]
	[Headers("Authorization: Bearer")]
	Task DeleteShift(Guid id);

	[Get("/shifts/{id}")]
	[Headers("Authorization: Bearer")]
	Task<ApiResponse<GetShiftResponse>> GetShift(Guid id);

	[Get("/shifts/available-types")]
	[Headers("Authorization: Bearer")]
	Task<ICollection<GetShiftTypesCountResponse>> GetAvailableShiftTypes(GetShiftTypesCountRequest request);


	[Put("/shifts/{id}")]
	[Headers("Authorization: Bearer")]
	Task UpdateShift(Guid id, CreateShiftRequest request);

	[Get("/containers/{id}")]
	[Headers("Authorization: Bearer")]
	Task<GetContainerResponse> GetContainer(Guid id);

	[Put("/locations/{id}")]
	[Headers("Authorization: Bearer")]
	Task UpdateLocation(Guid id, UpdateLocationRequest updateLocationRequest);

	[Get("/employees/{keycloakId}/shifts")]
	[Headers("Authorization: Bearer")]
	Task<IEnumerable<GetShiftResponse>> GetAllShiftsFromEmployee(Guid keycloakId, int count, DateTime? startingFrom, Guid seasonId);

	[Get("/locations/{Id}/get-all-available-shifts-types")]
	[Headers("Authorization: Bearer")]
	Task<IEnumerable<GetShiftTypesCountResponse>> GetAllAvailableShiftTypesFromLocationAsync(Guid id,
		GetAvailableShiftsForLocationRequest request);

	[Get("/employees")]
	[Headers("Authorization: Bearer")]
	Task<IEnumerable<GetEmployeeResponse>> GetAllEmployees();

	[Get("/locations/get-all-shifts-count")]
	[Headers("Authorization: Bearer")]
	Task<IEnumerable<GetShiftsCountResponse>> GetAllLocationShiftsCount(GetShiftsCountRequest request);

	[Get("/locations/{locationId}/get-shifts-count")]
	[Headers("Authorization: Bearer")]
	Task<GetShiftsCountResponse> GetLocationShiftsCount(Guid locationId);

	[Get("/employees/shifts-ics")]
	[Headers("Authorization: Bearer")]
	Task<HttpContent> GetAllShiftsFromEmployeeAsIcsFile(GetShiftsFromEmployeeRequest request);

	[Get("/shifts")]
	[Headers("Authorization: Bearer")]
	Task<List<GetShiftResponse>> GetAllShifts(GetAllShiftsRequest request);

	[Get("/seasons")]
	[Headers("Authorization: Bearer")]
	Task<IReadOnlyCollection<GetSeasonResponse>> GetAllSeasons();

	[Post("/seasons")]
	[Headers("Authorization: Bearer")]
	Task CreateSeason(CreateSeasonRequest request);

	[Put("/seasons/{EntityToEdit.Id}")]
	[Headers("Authorization: Bearer")]
	Task UpdateSeason(GetSeasonResponse entityToEdit);
	
	[Put("/seasons/current/{EntityToEdit.Id}")]
	[Headers("Authorization: Bearer")]
	Task SetCurrentSeason(DefaultGetRequest entityToEdit);


	[Delete("/seasons/{id}")]
	[Headers("Authorization: Bearer")]
	Task DeleteSeasons(Guid id);

	#region settings

	[Get("/settings")]
	Task<GetAppSettingsResponse> GetAppSettings();

	[Put("/settings")]
	[Headers("Authorization: Bearer")]
	Task UpdateAppSettings(UpdateAppSettingsRequest request);

	#endregion

	#region notifications

	[Get("/me/notifications")]
	[Headers("Authorization: Bearer")]
	Task<GetNotificationSettingsResponse> GetNotificationSettings();

	[Put("/me/notifications")]
	[Headers("Authorization: Bearer")]
	Task UpdateNotificationSettings(UpdateNotificationSettingsRequest request);

	[Post("/me/notifications/channels/{channel}/link")]
	[Headers("Authorization: Bearer")]
	Task<CreateChannelLinkResponse> CreateNotificationChannelLink(NotificationChannel channel);

	[Delete("/me/notifications/channels/{channel}")]
	[Headers("Authorization: Bearer")]
	Task DeleteNotificationChannel(NotificationChannel channel);

	#endregion
}

public class CustomUrlParameterFormatter : Refit.DefaultUrlParameterFormatter
{
	public override string? Format(object? parameterValue, ICustomAttributeProvider attributeProvider, Type type)
	{
		return parameterValue is DateTime dt
			? dt.ToUniversalTime().ToString("O")
			: base.Format(parameterValue, attributeProvider, type);
	}
}