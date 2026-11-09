using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.JSInterop;
using Muddi.ShiftPlanner.Client.Entities;

namespace Muddi.ShiftPlanner.Client.Services;

/// <summary>
/// Persists the <see cref="PendingShiftClaim"/> across the Keycloak round-trip and starts the login/registration.
/// </summary>
public sealed class PendingShiftClaimService
{
	public const string StorageKey = "pendingShiftClaim";

	private readonly IJSRuntime _js;
	private readonly NavigationManager _navigation;
	private readonly ILogger<PendingShiftClaimService> _logger;

	public PendingShiftClaimService(IJSRuntime js, NavigationManager navigation, ILogger<PendingShiftClaimService> logger)
	{
		_js = js;
		_navigation = navigation;
		_logger = logger;
	}

	public async Task SaveAsync(PendingShiftClaim claim)
		=> await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, claim.ToJson());

	/// <summary>The stored claim, or null if there is none or it expired (expired ones are removed).</summary>
	public async Task<PendingShiftClaim?> TryGetAsync()
	{
		try
		{
			var json = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
			var claim = PendingShiftClaim.TryParse(json, DateTime.UtcNow);
			if (claim is null && !string.IsNullOrEmpty(json))
				await ClearAsync();
			return claim;
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Could not read pending shift claim");
			return null;
		}
	}

	public async Task ClearAsync()
	{
		try
		{
			await _js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Could not clear pending shift claim");
		}
	}

	/// <summary>
	/// Sends the visitor to Keycloak and brings them back to <paramref name="returnUrl"/> (relative or absolute).
	/// With <paramref name="register"/> the OIDC <c>prompt=create</c> parameter asks Keycloak to open the
	/// registration form directly (supported since Keycloak 24; otherwise the login page offers its own link).
	/// </summary>
	public void NavigateToLogin(string returnUrl, bool register = false)
	{
		var options = new InteractiveRequestOptions
		{
			Interaction = InteractionType.SignIn,
			ReturnUrl = _navigation.ToAbsoluteUri(returnUrl).ToString()
		};
		if (register)
			options.TryAddAdditionalParameter("prompt", "create");
		_navigation.NavigateToLogin("authentication/login", options);
	}
}
