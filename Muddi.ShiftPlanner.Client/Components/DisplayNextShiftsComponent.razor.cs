using System.Web;
using Blazor.DownloadFileFast.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Muddi.ShiftPlanner.Client.Entities;
using Muddi.ShiftPlanner.Client.Pages.Locations;
using Muddi.ShiftPlanner.Client.Services;
using Muddi.ShiftPlanner.Shared.Entities;
using Radzen;

namespace Muddi.ShiftPlanner.Client.Components;

public partial class DisplayNextShiftsComponent : IDisposable
{
	[Inject] public required ILogger<DisplayNextShiftsComponent> Logger { get; set; }
	[Inject] public required ShiftService ShiftService { get; set; }
	[Inject] public required DialogService DialogService { get; set; }
	[Inject] public required IBlazorDownloadFileService BlazorDownloadFileService { get; set; }
	[CascadingParameter] public required Task<AuthenticationState> AuthStateTask { get; set; }
	private List<DayAppointment>? _myShifts;
	private List<DayAppointment>? _freeShifts;

	protected override async Task OnInitializedAsync()
	{
		await ShiftService.InitializedTask;
		ShiftService.OnSeasonChanged += OnSeasonChange;
		await Init();
	}

	private async void OnSeasonChange(object? sender, Season e)
	{
		try
		{
			await Init();
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "An error occurred while loading a list of available shifts");
		}
	}


	private async Task Init()
	{
		var authState = await AuthStateTask;
		if (authState.User.Identity?.IsAuthenticated == true)
		{
			var shifts = await ShiftService.GetAllShiftsFromUser(authState.User, 6, DateTime.UtcNow);
			_myShifts = new(shifts.Select(s => s.ToAppointment()));
		}
		else
		{
			// Anonymous visitors have no own shifts; the free ones come from the public API.
			_myShifts = [];
		}

		var availableShifts = await ShiftService.GetAllAvailableShifts(12, DateTime.UtcNow);
		_freeShifts = [..availableShifts.Select(s => s.ToAppointment())];
		await InvokeAsync(StateHasChanged);
	}

	private static string MakeLocationUri(DayAppointment appointment, bool showOnlyUserShift = true)
	{
		var query = HttpUtility.ParseQueryString(string.Empty);
		query[nameof(LocationPage.StartDate)] = appointment.LocalStartTime.ToString("yyyy-MM-dd");
		query[nameof(LocationPage.ShowOnlyUsersShifts)] = showOnlyUserShift.ToString();
		return $"/locations/{appointment.Shift.LocationId}/?{query}";
	}

	private async Task Download_iCal()
	{
		var bytes = await ShiftService.GetAllShiftsFromUserAsICal(ShiftService.CurrentSeason.Id);
		if (!bytes.Any())
		{
			await DialogService.Alert("Du hast leider noch keine Schichten");
			return;
		}

		await BlazorDownloadFileService.DownloadFileAsync("muddi-calendar.ics", bytes);
	}

	public void Dispose()
	{
		ShiftService.OnSeasonChanged -= OnSeasonChange;
	}
}