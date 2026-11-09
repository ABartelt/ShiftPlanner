using System.Collections.Immutable;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using Muddi.ShiftPlanner.Shared;
using Muddi.ShiftPlanner.Shared.Api;
using Muddi.ShiftPlanner.Shared.Contracts.v1.Requests;
using Muddi.ShiftPlanner.Shared.Contracts.v1.Responses;
using Muddi.ShiftPlanner.Shared.Entities;

namespace Muddi.ShiftPlanner.Client.Services;

/// <summary>
/// Facade over the API for the schedule views. Works in two modes, fixed by <see cref="Initialize"/>:
/// <list type="bullet">
/// <item><b>authenticated</b>: everything goes through <see cref="IMuddiShiftApi"/> (token attached)</item>
/// <item><b>public</b> (anonymous visitor, public browsing enabled): the read-only subset goes through
/// <see cref="IMuddiPublicShiftApi"/>; everything personal or writing throws <see cref="InvalidOperationException"/>
/// instead of accidentally triggering a login redirect.</item>
/// </list>
/// The OIDC login round-trip reloads the whole app, so the mode never has to switch at runtime.
/// </summary>
public class ShiftService
{
	private readonly IMuddiShiftApi _shiftApi;
	private readonly IMuddiPublicShiftApi _publicApi;
	private readonly AppSettingsService _appSettings;
	public Task InitializedTask => _initializedTsc.Task;
	private readonly TaskCompletionSource _initializedTsc = new();
	private readonly SemaphoreSlim _initalizeLock = new(1, 1);

	public Season CurrentSeason { get; private set; }
	public event EventHandler<Season>? OnSeasonChanged;

	/// <summary>True when serving an anonymous visitor from the public API.</summary>
	public bool IsPublicMode { get; private set; }

	public ShiftService(IMuddiShiftApi shiftApi, IMuddiPublicShiftApi publicApi, AppSettingsService appSettings)
	{
		_shiftApi = shiftApi;
		_publicApi = publicApi;
		_appSettings = appSettings;
		CurrentSeason = new();
	}

	private IEnumerable<ShiftLocation>? _shiftLocations;
	public IReadOnlyCollection<Season>? Seasons;

	public async Task Initialize(bool isAuthenticated)
	{
		await _initalizeLock.WaitAsync();
		try
		{
			var publicMode = !isAuthenticated;
			if (_initializedTsc.Task.IsCompleted && IsPublicMode == publicMode)
				return;
			// Mode changes should not happen (login reloads the app), but never keep serving public data to a
			// logged-in user or vice versa.
			IsPublicMode = publicMode;
			await LoadSeasonsAndCurrentAsync();
			_initializedTsc.TrySetResult();
		}
		finally
		{
			_initalizeLock.Release();
		}
	}

	private async Task LoadSeasonsAndCurrentAsync()
	{
		if (IsPublicMode)
		{
			var info = await _appSettings.GetPublicInfoAsync();
			if (info.CurrentSeason is null)
				throw new InvalidOperationException("Zurzeit gibt es keine aktive Saison.");
			Seasons = ImmutableList.Create(Season.FromResponse(info.CurrentSeason));
		}
		else
		{
			Seasons = (await _shiftApi.GetAllSeasons()).Select(Season.FromResponse).ToImmutableList();
		}

		await ChangeSeason(Seasons.FirstOrDefault(x => x.IsSelected) ?? Seasons.Last());
	}

	public async Task ChangeSeason(Season season)
	{
		CurrentSeason = season;
		IEnumerable<GetLocationResponse> dtos;
		IEnumerable<GetShiftsCountResponse> counts;
		if (IsPublicMode)
		{
			dtos = await _publicApi.GetAllLocations();
			counts = await _publicApi.GetAllLocationShiftsCount();
		}
		else
		{
			dtos = await _shiftApi.GetAllLocations(new GetLocationRequest { SeasonId = season.Id });
			counts = await _shiftApi.GetAllLocationShiftsCount(new() { SeasonId = season.Id });
		}

		_shiftLocations = dtos.Join(counts, d => d.Id, c => c.Id, (d, c) => d.MapToShiftLocation(c));
		OnSeasonChanged?.Invoke(this, season);
	}

	public IEnumerable<ShiftLocation> GetAllShiftLocations()
	{
		return _shiftLocations ?? [];
	}

	public ShiftLocation? FindLocationById(Guid id)
	{
		return _shiftLocations?.FirstOrDefault(l => l.Id == id);
	}

	/// <summary>
	/// Returns an ordered by StartTime list
	/// </summary>
	/// <param name="id"></param>
	/// <param name="start"></param>
	/// <param name="end"></param>
	/// <param name="limit"></param>
	/// <returns></returns>
	public async Task<IEnumerable<GetShiftTypesCountResponse>> GetAllAvailableShiftTypesFromLocationAsync(Guid id,
		DateTime? start = null,
		DateTime? end = null, int limit = -1)
	{
		var request = new GetAvailableShiftsForLocationRequest
		{
			LocationId = id,
			StartTime = start,
			EndTime = end,
			Limit = limit
		};
		return IsPublicMode
			? await _publicApi.GetAllAvailableShiftTypesFromLocationAsync(id, request)
			: await _shiftApi.GetAllAvailableShiftTypesFromLocationAsync(id, request);
	}

	/// <summary>Roster of a location (who booked what). Authenticated only.</summary>
	public async Task<IEnumerable<Shift>> GetAllShiftsFromLocationAsync(Guid id, DateTime start, DateTime end,
		Guid? forEmployeeId = null)
	{
		EnsureAuthenticatedMode();
		var dtos = await _shiftApi.GetAllShiftsForLocation(id,
			new() { Start = start, End = end, KeycloakEmployeeId = forEmployeeId });
		return dtos.Select(t => t.MapToShift());
	}

	public async Task<Guid> AddShiftToContainer(Guid containerId, CreateShiftRequest req)
	{
		EnsureAuthenticatedMode();
		var res = await _shiftApi.CreateShiftForContainer(containerId, req);
		return res.Id;
	}

	public async Task<IEnumerable<GetShiftTypesResponse>> GetAvailableShiftTypesAtTime(Guid containerId, DateTime start)
	{
		EnsureAuthenticatedMode();
		var availableShiftTypes = await _shiftApi.GetAvailableShiftTypes(containerId, start);
		return availableShiftTypes.Where(q => q.AvailableCount > 0).Select(q => q.Type);
	}

	public async Task<Shift?> GetShiftById(Guid id)
	{
		EnsureAuthenticatedMode();
		var resp = await _shiftApi.GetShift(id);
		if (resp.StatusCode == HttpStatusCode.NotFound)
			return null;
		if (resp.Error is not null)
			throw resp.Error;
		return resp.Content!.MapToShift();
	}

	/// <summary>Free/total places per slot and type for the whole current season (dashboard numbers).</summary>
	public async Task<ICollection<GetShiftTypesCountResponse>> GetAllShiftTypeCountsAsync(bool includeNonAvailable)
	{
		return IsPublicMode
			? await _publicApi.GetAvailableShiftTypes(new GetPublicAvailableShiftTypesRequest
			{
				IncludeNonAvailable = includeNonAvailable
			})
			: await _shiftApi.GetAvailableShiftTypes(new GetShiftTypesCountRequest
			{
				SeasonId = CurrentSeason.Id,
				IncludeNonAvailable = includeNonAvailable
			});
	}

	public async Task<IEnumerable<Shift>> GetAllAvailableShifts(int limit, DateTime? startingFrom = null)
	{
		ICollection<GetShiftTypesCountResponse> types = IsPublicMode
			? await _publicApi.GetAvailableShiftTypes(new GetPublicAvailableShiftTypesRequest
			{
				Limit = limit,
				StartingFrom = startingFrom
			})
			: await _shiftApi.GetAvailableShiftTypes(new GetShiftTypesCountRequest
			{
				Limit = limit,
				StartingFrom = startingFrom,
				SeasonId = CurrentSeason.Id
			});
		return types.Select(t => t.MapToShift());
	}

	public async Task<IEnumerable<Shift>> GetAllShiftsFromUser(ClaimsPrincipal user, int count = -1,
		DateTime? startingFrom = null)
	{
		EnsureAuthenticatedMode();
		var shifts =
			await _shiftApi.GetAllShiftsFromEmployee(user.GetKeycloakId(), count, startingFrom, CurrentSeason.Id);
		return shifts.Select(s => s.MapToShift());
	}

	public static void FillShiftsWithUnassignedShifts(List<Shift> shifts,
		IEnumerable<ShiftContainer> containers,
		DateTime startTime,
		DateTime endTime,
		Guid locationId)
	{
		startTime = startTime.ToUniversalTime();
		endTime = endTime.ToUniversalTime();

		foreach (var container in containers)
		{
			foreach (var containerStart in container.ShiftStartTimes)
			{
				if (containerStart < startTime || containerStart >= endTime)
					continue;
				foreach (var (type, count) in container.Framework.RolesCount)
				{
					var assignedShiftsCount = shifts.Count(s =>
						s.ContainerId == container.Id
						&& s.Type == type
						&& s.StartTime == containerStart);
					if (assignedShiftsCount < count)
					{
						for (int i = 0; i < count - assignedShiftsCount; i++)
						{
							shifts.Add(new Shift(Mappers.NotAssignedEmployee, containerStart,
								containerStart + container.Framework.TimePerShift,
								type, locationId, container.Id));
						}
					}
				}
			}
		}
	}

	/// <summary>
	/// Builds the day view for an anonymous visitor purely from free/total counts: per slot and shift type one
	/// "FREI" tile per free place and one anonymous "Besetzt" tile per taken place. No roster is involved, so no
	/// identity can leak. The container is resolved from the location's containers by start time and type.
	/// </summary>
	public static List<Shift> SynthesizeShiftsFromCounts(IEnumerable<GetShiftTypesCountResponse> counts,
		IReadOnlyList<ShiftContainer> containers, Guid locationId)
	{
		var result = new List<Shift>();
		foreach (var count in counts)
		{
			if (count.Type.OnlyAssignableByAdmin)
				continue; // the server strips these for anonymous callers; stay defensive

			var start = count.Start.ToUniversalTime();
			var end = count.End.ToUniversalTime();
			var type = count.Type.MapToShiftType();
			var container = containers.FirstOrDefault(c =>
				                c.ShiftStartTimes.Contains(start) && c.Framework.RolesCount.ContainsKey(type))
			                ?? containers.FirstOrDefault(c => c.IsTimeWithinContainer(start));
			var containerId = container?.Id ?? Guid.Empty;

			var free = Math.Max(0, Math.Min(count.AvailableCount, count.TotalCount));
			var taken = Math.Max(0, count.TotalCount - free);
			for (var i = 0; i < free; i++)
				result.Add(new Shift(Mappers.NotAssignedEmployee, start, end, type, locationId, containerId));
			for (var i = 0; i < taken; i++)
				result.Add(new Shift(Mappers.OccupiedEmployee, start, end, type, locationId, containerId));
		}

		return result;
	}

	public async Task<byte[]> GetAllShiftsFromUserAsICal(Guid seasonId)
	{
		EnsureAuthenticatedMode();
		var res = await _shiftApi.GetAllShiftsFromEmployeeAsIcsFile(new() { SeasonId = seasonId });
		return await res.ReadAsByteArrayAsync();
	}

	public async Task<ShiftLocation?> GetLocationsByIdAsync(Guid id)
	{
		GetLocationResponse dto;
		GetShiftsCountResponse count;
		if (IsPublicMode)
		{
			dto = await _publicApi.GetLocationById(id);
			count = await _publicApi.GetLocationShiftsCount(id);
		}
		else
		{
			dto = await _shiftApi.GetLocationById(id);
			count = await _shiftApi.GetLocationShiftsCount(id);
		}

		return dto.MapToShiftLocation(count);
	}

	private void EnsureAuthenticatedMode([CallerMemberName] string caller = "")
	{
		if (IsPublicMode)
			throw new InvalidOperationException($"{caller} ist ohne Anmeldung nicht verfügbar.");
	}
}
