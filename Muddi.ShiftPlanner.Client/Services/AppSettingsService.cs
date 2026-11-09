using System.Text.Json;
using Muddi.ShiftPlanner.Shared.Api;
using Muddi.ShiftPlanner.Shared.Contracts.v1.Requests;
using Muddi.ShiftPlanner.Shared.Contracts.v1.Responses;
using Muddi.ShiftPlanner.Shared.Entities;
using Microsoft.JSInterop;

namespace Muddi.ShiftPlanner.Client.Services;

/// <summary>
/// Provides app-wide settings (Title, Subtitle, Contact, StartTime, EndTime, Greeting, MemberName, ...).
/// Logged-in users load them from <c>GET /settings</c>; anonymous visitors from <c>GET /public/info</c>, which also
/// tells whether public browsing is enabled at all. Both are loaded once and cached. If the API is unreachable
/// the service falls back to <see cref="Defaults"/> (and "public browsing disabled") without caching the failure,
/// so a later call retries.
/// </summary>
public class AppSettingsService
{
	private const string LocalStorageKey = "appSettings";

	/// <summary>
	/// Values used until the real settings load, or whenever the API is unreachable.
	/// </summary>
	private static readonly ApplicationSettings Defaults = new();

	private static readonly JsonSerializerOptions JsonSerializerOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase
	};

	private readonly IMuddiShiftApi _api;
	private readonly IMuddiPublicShiftApi _publicApi;
	private readonly IJSRuntime _jsRuntime;
	private readonly ILogger<AppSettingsService> _logger;
	private Lazy<Task<ApplicationSettings>> _lazySettings;
	private Lazy<Task<GetPublicInfoResponse>> _lazyPublicInfo;


	/// <summary>
	/// Raised when the effective settings change — on the first successful load and on every update.
	/// </summary>
	public event EventHandler<ApplicationSettings>? SettingsChanged;

	/// <summary>
	/// The most recently known settings. Equals <see cref="Defaults"/> until the first successful load.
	/// </summary>
	public ApplicationSettings Current { get; private set; } = Defaults;

	/// <summary>
	/// Whether the server allows anonymous browsing. Null until <see cref="GetPublicInfoAsync"/> succeeded once.
	/// Intentionally not cached in localStorage: a stale "true" would flash public content after an admin turned it off.
	/// </summary>
	public bool? PublicBrowsingEnabled { get; private set; }

	public AppSettingsService(IMuddiShiftApi api, IMuddiPublicShiftApi publicApi, IJSRuntime jsRuntime,
		ILogger<AppSettingsService> logger)
	{
		_api = api;
		_publicApi = publicApi;
		_jsRuntime = jsRuntime;
		_logger = logger;
		_lazySettings = new Lazy<Task<ApplicationSettings>>(LoadAsync);
		_lazyPublicInfo = new Lazy<Task<GetPublicInfoResponse>>(LoadPublicInfoAsync);
	}

	/// <summary>
	/// Returns the current settings for a logged-in user, loading them from the API once. Falls back to
	/// <see cref="Defaults"/> if the API is unreachable; the failed load is not cached,
	/// so a later call retries instead of replaying the failure.
	/// </summary>
	public async ValueTask<ApplicationSettings> GetSettingsAsync()
	{
		var lazy = _lazySettings;
		try
		{
			return await lazy.Value;
		}
		catch
		{
			// Drop the faulted task so the next call retries instead of replaying the failure.
			if (_lazySettings == lazy)
				_lazySettings = new Lazy<Task<ApplicationSettings>>(LoadAsync);
			return Defaults;
		}
	}

	/// <summary>
	/// Anonymous entry point: flag, branding and current season from <c>GET /public/info</c>. Needs no token.
	/// On failure returns "disabled" so the app degrades to the usual login redirect.
	/// </summary>
	public async ValueTask<GetPublicInfoResponse> GetPublicInfoAsync()
	{
		var lazy = _lazyPublicInfo;
		try
		{
			return await lazy.Value;
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to load public info");
			if (_lazyPublicInfo == lazy)
				_lazyPublicInfo = new Lazy<Task<GetPublicInfoResponse>>(LoadPublicInfoAsync);
			return new GetPublicInfoResponse { PublicBrowsingEnabled = false };
		}
	}

	public async Task UpdateAsync(ApplicationSettings settings)
	{
		await _api.UpdateAppSettings(new UpdateAppSettingsRequest
		{
			Title = settings.Title,
			Subtitle = settings.Subtitle,
			Contact = settings.Contact,
			StartTime = settings.StartTime,
			EndTime = settings.EndTime,
			Greeting = settings.Greeting,
			MemberName = settings.MemberName,
			PublicBrowsingEnabled = settings.PublicBrowsingEnabled
		});

		// Re-fetch so callers and the SettingsChanged event see the persisted values.
		_lazySettings = new Lazy<Task<ApplicationSettings>>(LoadAsync);
		_lazyPublicInfo = new Lazy<Task<GetPublicInfoResponse>>(LoadPublicInfoAsync);
		await _lazySettings.Value;
	}

	private async Task<ApplicationSettings> LoadAsync()
	{
		var response = await _api.GetAppSettings();
		SetCurrent(response.Settings);
		await SaveToLocalStorageAsync(response.Settings);
		return response.Settings;
	}

	private async Task<GetPublicInfoResponse> LoadPublicInfoAsync()
	{
		var info = await _publicApi.GetInfo();
		PublicBrowsingEnabled = info.PublicBrowsingEnabled;
		if (info.Settings is { } settings)
		{
			SetCurrent(settings);
			await SaveToLocalStorageAsync(settings);
		}

		return info;
	}

	private async Task SaveToLocalStorageAsync(ApplicationSettings settings)
	{
		try
		{
			var json = JsonSerializer.Serialize(settings, JsonSerializerOptions);
			await _jsRuntime.InvokeVoidAsync("localStorage.setItem", LocalStorageKey, json);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to save settings to localStorage");
		}
	}

	/// <summary>
	/// Loads settings from localStorage, returning null if nothing is cached.
	/// </summary>
	public async Task<ApplicationSettings> GetCachedOrDefaultSettingsAsync()
	{
		try
		{
			var json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", LocalStorageKey);
			if (string.IsNullOrEmpty(json))
				return Defaults;
			return JsonSerializer.Deserialize<ApplicationSettings>(json, JsonSerializerOptions) ?? Defaults;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to load settings from localStorage");
			return Defaults;
		}
	}

	private void SetCurrent(ApplicationSettings settings)
	{
		if (Current == settings)
			return;
		Current = settings;
		SettingsChanged?.Invoke(this, settings);
	}
}
