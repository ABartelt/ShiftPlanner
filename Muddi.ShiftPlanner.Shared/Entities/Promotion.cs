using System.Text.Json.Serialization;

namespace Muddi.ShiftPlanner.Shared.Entities;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PromotionChannelKind
{
	Telegram,
	Slack,
	Signal
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PromotionRunKind
{
	Daily,
	Recruiting,
	LastCall,
	Manual,
	Test
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PromotionLogStatus
{
	Sent,
	Failed,
	Skipped
}

/// <summary>
/// Message flavor of a promotion text. Manual sharing (WhatsApp, plain copy) only needs a text,
/// automated channels map to their own format.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PromotionFormat
{
	Plain,
	WhatsApp,
	Signal,
	Telegram,
	Slack
}

/// <summary>
/// Non-secret settings of a promotion channel.
/// </summary>
public record PromotionChannelConfig
{
	/// <summary>
	/// Telegram chat id or Signal group id ("group.…"). Unused for Slack webhooks.
	/// </summary>
	public string Target { get; set; } = string.Empty;

	/// <summary>
	/// Display name of the target, as picked in the UI.
	/// </summary>
	public string TargetName { get; set; } = string.Empty;

	/// <summary>
	/// Signal: phone number of the linked bot account (E.164).
	/// </summary>
	public string SignalNumber { get; set; } = string.Empty;

	/// <summary>
	/// Signal: URL of the signal-cli-rest-api container.
	/// </summary>
	public string ServiceUrl { get; set; } = string.Empty;

	/// <summary>
	/// Only promote shifts at these locations; empty means all locations.
	/// </summary>
	public List<Guid> LocationIds { get; set; } = [];
}

/// <summary>
/// When open shifts of a season are posted automatically. Times are local to <see cref="TimeZone"/>.
/// </summary>
public record PromotionSchedule
{
	/// <summary>
	/// Master switch for automatic posting of this season.
	/// </summary>
	public bool Enabled { get; set; }

	/// <summary>
	/// IANA time zone of the event, e.g. "Europe/Berlin".
	/// </summary>
	public string TimeZone { get; set; } = "Europe/Berlin";

	/// <summary>
	/// Public URL of the web app, used to build links in the messages.
	/// </summary>
	public string PublicBaseUrl { get; set; } = string.Empty;

	/// <summary>
	/// Don't post when fewer shifts are open.
	/// </summary>
	public int MinOpenShifts { get; set; } = 1;

	/// <summary>
	/// During the event: post the open shifts of the next days every day.
	/// </summary>
	public bool DailyEnabled { get; set; } = true;

	public TimeSpan DailySendTime { get; set; } = new(19, 0, 0);

	/// <summary>
	/// How many days the daily post covers, starting tomorrow.
	/// </summary>
	public int DailyLookaheadDays { get; set; } = 1;

	/// <summary>
	/// Before the event: post a weekly summary of all open shifts.
	/// </summary>
	public bool RecruitingEnabled { get; set; }

	public DayOfWeek RecruitingDay { get; set; } = DayOfWeek.Monday;
	public TimeSpan RecruitingSendTime { get; set; } = new(18, 0, 0);

	/// <summary>
	/// Post shifts that are still open shortly before they start.
	/// </summary>
	public bool LastCallEnabled { get; set; }

	public int LastCallHoursBefore { get; set; } = 3;

	/// <summary>
	/// No last-call posts between these times.
	/// </summary>
	public TimeSpan QuietFrom { get; set; } = new(22, 0, 0);

	public TimeSpan QuietUntil { get; set; } = new(8, 0, 0);
}
