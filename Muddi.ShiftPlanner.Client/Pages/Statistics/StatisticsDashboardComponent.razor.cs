using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Muddi.ShiftPlanner.Client.Services;
using Muddi.ShiftPlanner.Shared.Api;
using Muddi.ShiftPlanner.Shared.Contracts.v1;
using Muddi.ShiftPlanner.Shared.Contracts.v1.Responses;
using Muddi.ShiftPlanner.Shared.Entities;
using Radzen;

namespace Muddi.ShiftPlanner.Client.Pages.Statistics;

public partial class StatisticsDashboardComponent : ComponentBase, IDisposable
{
	private string TotalUsers { get; set; } = "-";
	private string TotalShifts { get; set; } = "-";
	private string TotalDays { get; set; } = "-";
	private string TotalPercentage { get; set; } = "-";
	private int TotalShiftsCount { get; set; }
	private int AvailableCount { get; set; }
	private TimeSpan TotalTimeSpan { get; set; }
	private string MetricValueClass => _loading ? "metric-value skeleton" : "metric-value";
	private bool _loading = true;

	[Inject] public required ILogger<StatisticsDashboardComponent> Logger { get; set; }
	[Inject] public required TooltipService TooltipService { get; set; }
	[Inject] public required IMuddiShiftApi ShiftApi { get; set; }
	[Inject] public required ShiftService ShiftService { get; set; }
	[CascadingParameter] public required Task<AuthenticationState> AuthStateTask { get; set; }

	/// <summary>The member count comes from an admin-only endpoint; everybody else gets the shift numbers only.</summary>
	private bool CanSeeMembers { get; set; }


	protected override async Task OnInitializedAsync()
	{
		var authState = await AuthStateTask;
		CanSeeMembers = authState.User.IsInRole(ApiRoles.Admin);
		await ShiftService.InitializedTask;
		ShiftService.OnSeasonChanged += RefreshData;
		await LoadDashboardData(ShiftService.CurrentSeason);
	}

	private void ShowTooltip(ElementReference elementReference, string text, TooltipOptions? options = null)
		=> TooltipService.Open(elementReference, text, options ?? new TooltipOptions
		{
			Position = TooltipPosition.Bottom, Style = "text-wrap: wrap; max-width:150px", Duration = 60000
		});


	protected override bool ShouldRender() => !_loading;

	private async Task LoadDashboardData(Season season)
	{
		_loading = true;
		if (season.Id == Guid.Empty)
			return;

		try
		{
			var allAvailableShifts = await ShiftService.GetAllShiftTypeCountsAsync(includeNonAvailable: true);

			var totalShiftHours = CalculateTotalTime(allAvailableShifts);

			AvailableCount = 0;
			TotalShiftsCount = 0;
			foreach (var resp in allAvailableShifts)
			{
				AvailableCount += resp.AvailableCount;
				TotalShiftsCount += resp.TotalCount;
			}

			if (CanSeeMembers)
			{
				try
				{
					var users = await ShiftApi.GetAllEmployees();
					TotalUsers = users.Count().ToString();
				}
				catch (Exception ex)
				{
					Logger.LogWarning(ex, "Could not load member count");
				}
			}

			TotalShifts = (TotalShiftsCount - AvailableCount).ToString();
			TotalDays = totalShiftHours.TotalDays.ToString("N1");
			TotalTimeSpan = totalShiftHours;
			if (TotalShiftsCount > 0)
				TotalPercentage = (100 * (TotalShiftsCount - AvailableCount) / TotalShiftsCount).ToString("N0");
		}

		catch (Exception ex)
		{
			// Handle error appropriately
			Console.WriteLine($"Error loading dashboard data: {ex.Message}");
		}

		_loading = false;

		await InvokeAsync(StateHasChanged);
	}


	private static TimeSpan CalculateTotalTime(ICollection<GetShiftTypesCountResponse> shifts)
	{
		var timespan = TimeSpan.Zero;
		foreach (var shift in shifts)
		{
			timespan += (shift.End - shift.Start) * (shift.TotalCount - shift.AvailableCount);
		}

		return timespan;
	}


	private async void RefreshData(object? caller, Season season)
	{
		try
		{
			await LoadDashboardData(season);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Error loading dashboard data for season {SeasonId}", season.Id);
		}
	}

	public void Dispose()
	{
		ShiftService.OnSeasonChanged -= RefreshData;
	}
}