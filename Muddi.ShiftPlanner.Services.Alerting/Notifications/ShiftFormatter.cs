using Microsoft.Extensions.Options;
using Muddi.ShiftPlanner.Server.Database.Entities;
using Muddi.ShiftPlanner.Services.Alerting.Options;
using Muddi.ShiftPlanner.Shared.Contracts.v1;
using Muddi.ShiftPlanner.Shared.Extensions;

namespace Muddi.ShiftPlanner.Services.Alerting.Notifications;

/// <summary>
/// Turns shift entities into local times and links.
/// </summary>
public class ShiftFormatter
{
	private readonly string _clientBaseUrl;
	private readonly string _publicBaseUrl;

	public ShiftFormatter(IOptions<NotificationOptions> options, IConfiguration configuration,
		ILogger<ShiftFormatter> logger)
	{
		Zone = FindZone(options.Value.TimeZone, logger);
		_clientBaseUrl = configuration["MuddiShiftClient:BaseUrl"]?.TrimEnd('/') ?? string.Empty;
		_publicBaseUrl = options.Value.PublicBaseUrl.TrimEnd('/');
	}

	public ShiftFormatter(TimeZoneInfo zone, string clientBaseUrl, string publicBaseUrl)
	{
		Zone = zone;
		_clientBaseUrl = clientBaseUrl.TrimEnd('/');
		_publicBaseUrl = publicBaseUrl.TrimEnd('/');
	}

	public TimeZoneInfo Zone { get; }

	public DateTime ToLocal(DateTime utc)
		=> TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

	/// <summary>
	/// Location page at the given day; the Blazor client reads StartDate, the Vue client date.
	/// </summary>
	public string LocationDayLink(Guid locationId, DateTime localDay)
	{
		var day = localDay.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
		return $"{_clientBaseUrl}/locations/{locationId}?date={day}&StartDate={day}";
	}

	public string? PublicUrl(string path) => string.IsNullOrEmpty(_publicBaseUrl) ? null : _publicBaseUrl + path;

	/// <summary>
	/// Needs Type, ShiftContainer.Location and ShiftContainer.Framework loaded.
	/// </summary>
	public ShiftSummary Summarize(ShiftEntity shift)
	{
		// End is not updated when a shift is moved, the framework's slot length is reliable
		var length = shift.ShiftContainer.Framework?.TimePerShift ?? shift.End - shift.Start;
		var start = ToLocal(ShiftRules.EffectiveStart(shift.Start, shift.Type.StartingTimeShift));
		var end = ToLocal(ShiftRules.EffectiveStart(shift.Start + length, shift.Type.StartingTimeShift));
		return new ShiftSummary(shift.Type.Name, shift.ShiftContainer.Location.Name, start, end,
			LocationDayLink(shift.ShiftContainer.Location.Id, start.Date));
	}

	public ReminderMessage CreateReminder(ShiftNotificationEntity notification, ShiftEntity shift, bool canRelease,
		string? adminContact, string? unsubscribeToken)
	{
		var summary = Summarize(shift);
		return new ReminderMessage
		{
			NotificationId = notification.Id,
			Kind = notification.Kind,
			ShiftTypeName = summary.ShiftTypeName,
			LocationName = summary.LocationName,
			StartLocal = summary.StartLocal,
			EndLocal = summary.EndLocal,
			ShiftLink = summary.ShiftLink,
			CanRelease = canRelease,
			ActionCode = notification.ActionCode,
			ReleaseUrl = canRelease && notification.ActionToken is not null
				? PublicUrl($"/actions/release/{notification.ActionToken}")
				: null,
			UnsubscribeUrl = notification.Channel == NotificationChannel.Email && unsubscribeToken is not null
				? PublicUrl($"/actions/unsubscribe/{unsubscribeToken}")
				: null,
			AdminContact = string.IsNullOrWhiteSpace(adminContact) ? null : adminContact
		};
	}

	private static TimeZoneInfo FindZone(string id, ILogger logger)
	{
		try
		{
			return TimeZoneInfo.FindSystemTimeZoneById(id);
		}
		catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
		{
			logger.LogWarning("Time zone {TimeZone} not found (is tzdata installed?), using UTC", id);
			return TimeZoneInfo.Utc;
		}
	}
}
