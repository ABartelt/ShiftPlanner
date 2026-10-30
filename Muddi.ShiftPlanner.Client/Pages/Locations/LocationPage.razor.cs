using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.JSInterop;
using Muddi.ShiftPlanner.Client.Components;
using Muddi.ShiftPlanner.Client.Entities;
using Muddi.ShiftPlanner.Client.Services;
using Muddi.ShiftPlanner.Client.Shared;
using Muddi.ShiftPlanner.Shared;
using Muddi.ShiftPlanner.Shared.Contracts.v1;
using Muddi.ShiftPlanner.Shared.Contracts.v1.Responses;
using Muddi.ShiftPlanner.Shared.Entities;
using Radzen;
using Radzen.Blazor;
using Radzen.Blazor.Rendering;
using Appointment = Muddi.ShiftPlanner.Client.Entities.Appointment;

namespace Muddi.ShiftPlanner.Client.Pages.Locations;

public partial class LocationPage
{
	[CascadingParameter] public required ApplicationSettings Settings { get; set; }

	private string RadzenStyle
		=> $"height: {CaclculateHeight()}px !important;";

	private string CaclculateHeight()
	{
		var hours = (Settings.EndTime - Settings.StartTime).TotalHours;
		return (48.125 * hours + 88.75).ToInvariantString();
	}

	[Inject] private DialogService DialogService { get; set; } = default!;
	[Inject] private NotificationService NotificationService { get; set; } = default!;
	[Inject] private ShiftService ShiftService { get; set; } = default!;
	[Inject] private PendingShiftClaimService PendingClaims { get; set; } = default!;
	[Inject] private ILogger<LocationPage> Logger { get; set; } = default!;
	[Inject] private IJSRuntime JsRuntime { get; set; } = default!;

	/// <summary>
	/// LocationId
	/// </summary>
	[Parameter]
	public Guid Id { get; set; }

	[SupplyParameterFromQuery] [Parameter] public bool ShowOnlyUsersShifts { get; set; }
	[SupplyParameterFromQuery] [Parameter] public int SelectedViewIndex { get; set; }

	public int GetSelectedViewIndex() => _scheduler is null
		? -1
		: _scheduler.IsSelected(_dayView)
			? DayViewIndex
			: _scheduler.IsSelected(_weekView)
				? WeekViewIndex
				: -1;

	[SupplyParameterFromQuery]
	[Parameter]
	public DateTime StartDate
	{
		get => _startDate;
		set => _startDate = value == default ? _startDate : value;
	}


	[CascadingParameter] public Task<AuthenticationState> AuthenticationState { get; set; }
	[CascadingParameter] public MainLayout MainLayout { get; set; } = default!;

	private ShiftLocation? _location;
	private ClaimsPrincipal? _user;

	private IEnumerable<Appointment> Shifts => ShowOnlyUsersShifts && _userKeycloakId is { } me
		? _shifts.Where(s => (s as DayAppointment)?.Shift?.User.KeycloakId == me)
		: _shifts;


	private RadzenScheduler<Appointment>? _scheduler;
	private IList<Appointment> _shifts = new List<Appointment>();

	/// <summary>The viewer's Keycloak id, or null for anonymous visitors.</summary>
	private Guid? _userKeycloakId;

	private bool _isAuthenticated;

	/// <summary>Free/total counts of this location (anonymous day view and claim dialog are built from these).</summary>
	private List<GetShiftTypesCountResponse> _publicCounts = [];

	protected override async Task OnParametersSetAsync()
	{
		if (_location?.Id == Id)
			return;
		try
		{
			await ShiftService.InitializedTask;
			var state = await AuthenticationState;
			_user = state.User;
			_isAuthenticated = _user.Identity?.IsAuthenticated == true;
			_userKeycloakId = _isAuthenticated ? _user.GetKeycloakId() : null;
			_isAdmin = _isAuthenticated && _user.IsInRole(ApiRoles.Admin);
			if (!_isAuthenticated)
				ShowOnlyUsersShifts = false;

			_location = await ShiftService.GetLocationsByIdAsync(Id);
			if (_location is null)
				throw new NullReferenceException($"Location with id {Id} not found");
			var firstAvailableShift =
				(await ShiftService.GetAllAvailableShiftTypesFromLocationAsync(Id, ShiftService.CurrentSeason.StartDate,
					ShiftService.CurrentSeason.EndDate, 1)).ToList();
			MainLayout.SetTitle($"{_location.Name} ({_location.AssignedShifts}/{_location.TotalShifts} Schichten)");

			var firstShiftStartDate = firstAvailableShift.FirstOrDefault()?.Start;
			if (_startDate == default || _startDate < firstShiftStartDate)
			{
				var startDate =
					(firstShiftStartDate ?? ShiftService.CurrentSeason.StartDate).ToLocalTime();
				_startDate = DateTime.Now > startDate ? DateTime.Now : startDate;
			}

			await SetShifts(SelectedViewIndex);

			if (_isAuthenticated && await PendingClaims.TryGetAsync() is { } claim && claim.LocationId == Id)
				await ResumeClaimAsync(claim);
		}
		catch (Exception ex)
		{
			if (ex is AccessTokenNotAvailableException)
				throw;
			await DialogService.Error(ex, "Error while setting parameter of places page");
		}
	}

	Task ForceReloadScheduler()
	{
		_shifts.Clear();
		return _scheduler?.Reload() ?? Task.CompletedTask;
	}

	private async Task OnSlotSelect(DateTime startTime, Shift shift = null)
	{
		if (_location is null || _user is null)
			return;
		if (shift.Type is { OnlyAssignableByAdmin: true } && !_isAdmin)
		{
			await DialogService.Confirm($"Leider dürfen nur Admins '{shift.Type.Name}' Nutzer:innen auswählen.");
			return;
		}

		startTime = startTime.ToUniversalTime();
		var container = _location.Containers.FirstOrDefault(x => x.Id == shift.ContainerId);
		if (container is null)
		{
			Logger.LogWarning("No container within this start time {Start}", startTime);
			return;
		}

		startTime = container.GetBestShiftStartTimeForTime(startTime).ToUniversalTime();

		if (!_isAuthenticated)
		{
			await OpenClaimLoginDialog(container, startTime, shift.Type);
			return;
		}

		var shiftResponse = new GetShiftResponse
		{
			ContainerId = container.Id,
			EmployeeId = _userKeycloakId!.Value,
			EmployeeFullName = _user.GetFullName(),
			Start = startTime,
			End = startTime + container.Framework.TimePerShift,
			Type = shift.Type.MapToShiftTypeResponse(),
			BlockReason = null
		};
		var param = new Dictionary<string, object>
		{
			[nameof(EditShiftDialog.EntityToEdit)] = shiftResponse
		};
		var res = await DialogService.OpenAsync<EditShiftDialog>("Neue Schicht", param);
		if (res is true)
		{
			try
			{
				await ForceReloadScheduler();
			}
			catch (Exception ex)
			{
				await DialogService.Error(ex);
			}
		}
	}

	/// <summary>
	/// Anonymous visitor clicked a free slot: let them pick the shift type, then send them to login/registration.
	/// The choice is stored as a <see cref="PendingShiftClaim"/> and resumed in <see cref="ResumeClaimAsync"/>.
	/// </summary>
	private async Task OpenClaimLoginDialog(ShiftContainer container, DateTime startTime, ShiftType clickedType)
	{
		var availableTypes = _publicCounts
			.Where(c => c.Start.ToUniversalTime() == startTime && c.AvailableCount > 0 && !c.Type.OnlyAssignableByAdmin)
			.Select(c => c.Type)
			.DistinctBy(t => t.Id)
			.ToList();
		if (availableTypes.Count == 0)
			availableTypes.Add(clickedType.MapToShiftTypeResponse());

		var param = new Dictionary<string, object>
		{
			[nameof(ClaimShiftLoginDialog.LocationId)] = _location!.Id,
			[nameof(ClaimShiftLoginDialog.LocationName)] = _location.Name,
			[nameof(ClaimShiftLoginDialog.ContainerId)] = container.Id,
			[nameof(ClaimShiftLoginDialog.Start)] = startTime,
			[nameof(ClaimShiftLoginDialog.End)] = startTime + container.Framework.TimePerShift,
			[nameof(ClaimShiftLoginDialog.AvailableTypes)] = availableTypes,
			[nameof(ClaimShiftLoginDialog.SelectedType)] = clickedType.MapToShiftTypeResponse()
		};
		await DialogService.OpenAsync<ClaimShiftLoginDialog>("Schicht eintragen", param);
	}

	/// <summary>
	/// Back from Keycloak with a stored claim for this location: re-check availability and open the normal
	/// "Neue Schicht" dialog prefilled. The user still has to confirm; nothing is booked automatically.
	/// </summary>
	private async Task ResumeClaimAsync(PendingShiftClaim claim)
	{
		await PendingClaims.ClearAsync(); // one-shot, whatever happens next
		if (_location is null || _user is null)
			return;

		var container = _location.Containers.FirstOrDefault(c => c.Id == claim.ContainerId);
		if (container is null)
		{
			await DialogService.Alert("Diese Schicht gibt es nicht mehr. Such dir gern eine andere aus!", "Schade");
			return;
		}

		var available = (await ShiftService.GetAvailableShiftTypesAtTime(container.Id, claim.Start))
			.Where(t => _isAdmin || !t.OnlyAssignableByAdmin)
			.ToList();
		if (available.Count == 0)
		{
			await DialogService.Alert("Schade – diese Schicht wurde gerade vergeben. Such dir gern eine andere aus!",
				"Schicht vergeben");
			return;
		}

		var type = available.FirstOrDefault(t => t.Id == claim.ShiftTypeId);
		if (type is null)
			NotificationService.Notify(NotificationSeverity.Warning, "Schichtart vergeben",
				$"„{claim.ShiftTypeName}“ ist inzwischen vergeben – du kannst eine andere Schichtart wählen.");
		else
			NotificationService.Notify(NotificationSeverity.Info, "Willkommen zurück!", "Bestätige kurz deine Schicht.");

		var shiftResponse = new GetShiftResponse
		{
			ContainerId = container.Id,
			EmployeeId = _userKeycloakId!.Value,
			EmployeeFullName = _user.GetFullName(),
			Start = claim.Start,
			End = claim.Start + container.Framework.TimePerShift,
			Type = type ?? available.First(),
			BlockReason = null
		};
		var param = new Dictionary<string, object>
		{
			[nameof(EditShiftDialog.EntityToEdit)] = shiftResponse
		};
		var res = await DialogService.OpenAsync<EditShiftDialog>("Neue Schicht", param);
		if (res is true)
			await ForceReloadScheduler();
	}

	private async Task OnShiftSelect(SchedulerAppointmentSelectEventArgs<Appointment> args)
	{
		//If appoint has no date and is WeekView, go to DayView of selected appointment
		if (args.Data is WeekAppointment && SelectedViewIndex == WeekViewIndex)
		{
			SelectedViewIndex = DayViewIndex;
			StartDate = args.Start.Date;
			await SetShifts(SelectedViewIndex, true);
			return;
		}

		if (args.Data is not DayAppointment dayAppointment)
			throw new NotSupportedException("Unknown appointment type" + args.Data.GetType());

		// Anonymous view: taken places are inert
		if (dayAppointment.Shift.User == Mappers.OccupiedEmployee)
			return;

		//If the shift is blocked (has BlockReason), open edit dialog
		if (dayAppointment.Shift.IsBlocked)
		{
			var blockedParams = new Dictionary<string, object?>
			{
				[nameof(EditShiftDialog.EntityToEdit)] = dayAppointment.Shift.MapToShiftResponse()
			};
			var blockedRes = await DialogService.OpenAsync<EditShiftDialog>("Bearbeite Schicht", blockedParams);
			if (blockedRes is true)
			{
				await ForceReloadScheduler();
			}

			return;
		}

		//If the user is not assigned, create a new shift
		if (dayAppointment.Shift.User == Mappers.NotAssignedEmployee)
		{
			await OnSlotSelect(args.Start, dayAppointment.Shift);
			return;
		}

		var param = new Dictionary<string, object>
		{
			[nameof(EditShiftDialog.EntityToEdit)] = dayAppointment.Shift.MapToShiftResponse()
		};
		var res = await DialogService.OpenAsync<EditShiftDialog>("Bearbeite Schicht", param);
		if (res is true)
		{
			await ForceReloadScheduler();
		}
	}

	// Never call StateHasChanged in AppointmentRender - would lead to infinite loop
	//TODO[perf] OnAppointmentRender will be called twice, on day/week leave and on day/week come
	private void OnAppointmentRender(SchedulerAppointmentRenderEventArgs<Appointment> args)
	{
		args.SetAppointmentRenderStyle(_userKeycloakId);
	}


	private void OnSlotRender(SchedulerSlotRenderEventArgs args)
		=> args.SetSlotRenderStyle(_location!.Containers);


	private readonly SemaphoreSlim _semaphore = new(1, 1);

	private async Task SetShifts(int idx, bool invokeStateHasChanged = false)
	{
		await _semaphore.WaitAsync();
		try
		{
			_shifts.Clear();
			var start = ShiftService.CurrentSeason.StartDate;
			var end = ShiftService.CurrentSeason.EndDate;
			switch (idx)
			{
				case WeekViewIndex:
				{
					//Week view
					var myShifts = _isAuthenticated
						? (await ShiftService.GetAllShiftsFromLocationAsync(Id, start, end, _userKeycloakId)).ToList()
						: [];
					var shiftTypes =
						await ShiftService.GetAllAvailableShiftTypesFromLocationAsync(Id, start, end);
					var group = shiftTypes.GroupBy(st => new { st.Start, st.End });
					var appointments = group.Select(g => CreateNewAppointment(g.Key.Start, g.Key.End, g, myShifts));
					_shifts = appointments.ToList();
					SelectedViewIndex = 1;
					UpdateQueryUri();
					break;
				}
				case DayViewIndex:
				{
					//day view
					List<Shift> shifts;
					if (_isAuthenticated)
					{
						shifts = (await ShiftService.GetAllShiftsFromLocationAsync(Id, start, end)).ToList();
						ShiftService.FillShiftsWithUnassignedShifts(shifts, _location!.Containers, start, end,
							_location.Id);
					}
					else
					{
						// No roster for anonymous visitors: build FREI/Besetzt tiles from counts only
						_publicCounts = (await ShiftService.GetAllAvailableShiftTypesFromLocationAsync(Id, start, end))
							.ToList();
						shifts = ShiftService.SynthesizeShiftsFromCounts(_publicCounts, _location!.Containers,
							_location.Id);
					}

					_shifts = shifts
						.OrderBy(q => q.Type.Name)
						.Select(Appointment (s) => s.ToAppointment())
						.ToList();
					SelectedViewIndex = 0;
					UpdateQueryUri();
					break;
				}
			}

			if (invokeStateHasChanged)
				await InvokeAsync(StateHasChanged);
		}
		finally
		{
			_semaphore.Release();
		}
	}

	private Appointment CreateNewAppointment(DateTime start, DateTime end,
		IEnumerable<GetShiftTypesCountResponse> getShiftTypesResponses, List<Shift> myShifts)
	{
		if (myShifts.FirstOrDefault(s => s.StartTime == start) is { } shift)
		{
			return shift.ToAppointment();
		}

		if (!_isAdmin)
			getShiftTypesResponses = getShiftTypesResponses.Where(s => s.Type.OnlyAssignableByAdmin == false);

		var arr = getShiftTypesResponses as GetShiftTypesCountResponse[] ?? getShiftTypesResponses.ToArray();
		var total = arr.Sum(s => s.TotalCount);
		var available = Math.Max(0, arr.Sum(s => s.AvailableCount));
		// var available = Random.Shared.Next(0, total + 1);
		var title = $"{available}/{total}\nfreie Schicht{(available != 1 ? "en" : "")}";
		return new WeekAppointment(start, end, title, available, total);
	}


	private bool _isAdmin;
	private DateTime _startDate;
	private RadzenDayView _dayView;
	private RadzenWeekView _weekView;
	[Inject] private NavigationManager NavigationManager { get; set; }

	private void UpdateQueryUri(DateTime date = default)
	{
		if (_scheduler is null)
			return;
		StartDate = (date == default ? _scheduler.CurrentDate.Date : date);
		var queryParams = new Dictionary<string, object?>
		{
			[nameof(ShowOnlyUsersShifts)] = ShowOnlyUsersShifts,
			[nameof(StartDate)] = StartDate.ToString("yyyy-MM-dd"),
			[nameof(SelectedViewIndex)] = SelectedViewIndex
		};
		_ = JsRuntime.InvokeVoidAsync("updateQueryParameters", queryParams).AsTask();
		InvokeAsync(StateHasChanged); //For the DatePicker
	}

	private void ShowOnlyUserShiftsButtonPressed()
	{
		ShowOnlyUsersShifts = !ShowOnlyUsersShifts;
		UpdateQueryUri();
	}

	private async Task Swipe(SwipeEvent obj)
	{
		if (_scheduler is null)
			return;
		ISchedulerView? view = GetSelectedViewIndex() switch
		{
			DayViewIndex => _dayView,
			WeekViewIndex => _weekView,
			_ => null
		};
		if (view is null)
			return;
		_scheduler.CurrentDate = obj switch
		{
			SwipeEvent.Left => view.Next(),
			SwipeEvent.Right => view.Prev(),
			_ => _scheduler.CurrentDate
		};
		await _scheduler.Reload();
	}

	private const int WeekViewIndex = 1;
	private const int DayViewIndex = 0;

	private void LoadShifts(SchedulerLoadDataEventArgs obj)
	{
		UpdateQueryUri();
		var current = GetSelectedViewIndex();
		if (SelectedViewIndex == current && _shifts.Count > 0)
			return;
		_ = SetShifts(current, true);
	}
}
