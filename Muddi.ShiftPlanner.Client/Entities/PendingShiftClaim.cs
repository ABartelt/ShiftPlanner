using System.Text.Json;
using System.Text.Json.Serialization;

namespace Muddi.ShiftPlanner.Client.Entities;

/// <summary>
/// The shift an anonymous visitor chose before being sent to Keycloak. Stored in localStorage so it survives the
/// login/registration round-trip (including e-mail verification in a new tab) and is resumed on the location page
/// after login. Holds no personal data.
/// </summary>
public sealed record PendingShiftClaim(
	Guid LocationId,
	Guid ContainerId,
	DateTime Start,
	Guid ShiftTypeId,
	string LocationName,
	string ShiftTypeName,
	DateTime CreatedAt)
{
	public static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

	private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
	{
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
	};

	public static PendingShiftClaim Create(Guid locationId, Guid containerId, DateTime startUtc, Guid shiftTypeId,
		string locationName, string shiftTypeName, DateTime utcNow)
		=> new(locationId, containerId, DateTime.SpecifyKind(startUtc.ToUniversalTime(), DateTimeKind.Utc),
			shiftTypeId, locationName, shiftTypeName, DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));

	public bool IsExpired(DateTime utcNow) => utcNow - CreatedAt > Ttl;

	public string ToJson() => JsonSerializer.Serialize(this, Json);

	/// <summary>Null for empty, invalid or expired input.</summary>
	public static PendingShiftClaim? TryParse(string? json, DateTime utcNow)
	{
		if (string.IsNullOrWhiteSpace(json))
			return null;
		try
		{
			var claim = JsonSerializer.Deserialize<PendingShiftClaim>(json, Json);
			if (claim is null || claim.LocationId == Guid.Empty || claim.ShiftTypeId == Guid.Empty)
				return null;
			claim = claim with
			{
				Start = claim.Start.ToUniversalTime(),
				CreatedAt = claim.CreatedAt.ToUniversalTime()
			};
			return claim.IsExpired(utcNow) ? null : claim;
		}
		catch (JsonException)
		{
			return null;
		}
	}

	/// <summary>Location page URL that shows the day the visitor was looking at (local calendar date).</summary>
	public string BuildReturnPath(DateTime localDate)
		=> $"/locations/{LocationId}?StartDate={localDate:yyyy-MM-dd}&SelectedViewIndex=0&ShowOnlyUsersShifts=False";
}
