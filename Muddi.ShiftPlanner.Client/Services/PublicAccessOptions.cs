namespace Muddi.ShiftPlanner.Client.Services;

/// <summary>
/// Client-side configuration for the anonymous experience (section <c>OAuth</c> in appsettings.json).
/// </summary>
/// <param name="AllowRegistration">
/// Show "Registrieren" buttons that send the visitor to Keycloak's registration form. Only the realm knows whether
/// self-registration is enabled, so the operator mirrors that here.
/// </param>
public sealed record PublicAccessOptions(bool AllowRegistration);
