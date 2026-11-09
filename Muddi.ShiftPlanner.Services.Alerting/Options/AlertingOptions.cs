namespace Muddi.ShiftPlanner.Services.Alerting.Options;

public class NotificationOptions
{
	/// <summary>
	/// IANA time zone used for all times in messages.
	/// </summary>
	public string TimeZone { get; set; } = "Europe/Berlin";

	public TimeSpan PollInterval { get; set; } = TimeSpan.FromMinutes(1);
	public TimeSpan DayBeforeLead { get; set; } = TimeSpan.FromDays(1);
	public TimeSpan ShortBeforeLead { get; set; } = TimeSpan.FromMinutes(30);

	/// <summary>
	/// How often a failed reminder is tried while it is still due.
	/// </summary>
	public int MaxAttempts { get; set; } = 3;

	/// <summary>
	/// How long sent reminders are kept, they prevent double sends.
	/// </summary>
	public TimeSpan Retention { get; set; } = TimeSpan.FromDays(60);

	/// <summary>
	/// Public URL of this service; used for the e-mail action links and to validate Twilio webhooks.
	/// </summary>
	public string PublicBaseUrl { get; set; } = string.Empty;
}

public class TelegramOptions
{
	public string ApiToken { get; set; } = string.Empty;
	public string BotUsername { get; set; } = string.Empty;

	/// <summary>
	/// Group that gets the daily summary of free shifts; 0 disables the summary.
	/// </summary>
	public long GroupId { get; set; }

	/// <summary>
	/// First day of the event (dd.MM.yyyy); before it the summary is about that day.
	/// </summary>
	public string? StartDate { get; set; }

	/// <summary>
	/// UTC time of the daily summary.
	/// </summary>
	public TimeSpan SendTime { get; set; } = new(19, 0, 0);

	public bool SendOnProgramStart { get; set; }

	public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiToken);
}

public class TwilioOptions
{
	public string AccountSid { get; set; } = string.Empty;
	public string AuthToken { get; set; } = string.Empty;

	/// <summary>
	/// WhatsApp sender in E.164, e.g. +4915112345678.
	/// </summary>
	public string WhatsAppFrom { get; set; } = string.Empty;

	public TwilioTemplateOptions Templates { get; set; } = new();

	public bool IsConfigured => !string.IsNullOrWhiteSpace(AccountSid)
	                            && !string.IsNullOrWhiteSpace(AuthToken)
	                            && !string.IsNullOrWhiteSpace(WhatsAppFrom);
}

/// <summary>
/// Content SIDs (HX...) of approved WhatsApp templates. Reminders are sent outside the 24h session window,
/// so WhatsApp only delivers them as templates. Without a SID a free-form message is sent (Twilio sandbox).
/// </summary>
public class TwilioTemplateOptions
{
	/// <summary>
	/// Variables: 1 type, 2 location, 3 time, 4 link, 5 quick-reply id of the "Schicht abgeben" button.
	/// </summary>
	public string DayBefore { get; set; } = string.Empty;

	/// <summary>
	/// Day-before reminder without the release button (shift is already locked); falls back to <see cref="DayBefore"/>.
	/// </summary>
	public string DayBeforeNoRelease { get; set; } = string.Empty;

	/// <summary>
	/// Variables: 1 type, 2 location, 3 time, 4 link.
	/// </summary>
	public string ShortBefore { get; set; } = string.Empty;
}

public class SignalOptions
{
	/// <summary>
	/// URL of the signal-cli-rest-api container (normal or native mode).
	/// </summary>
	public string BaseUrl { get; set; } = string.Empty;

	/// <summary>
	/// Registered Signal number in E.164.
	/// </summary>
	public string Number { get; set; } = string.Empty;

	public TimeSpan ReceiveInterval { get; set; } = TimeSpan.FromSeconds(10);

	public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(Number);
}

public class SmtpOptions
{
	public string Host { get; set; } = string.Empty;
	public int Port { get; set; } = 587;

	/// <summary>
	/// MailKit SecureSocketOptions: None, Auto, SslOnConnect, StartTls, StartTlsWhenAvailable.
	/// </summary>
	public string Security { get; set; } = "StartTls";

	public string? User { get; set; }
	public string? Password { get; set; }
	public string FromAddress { get; set; } = string.Empty;
	public string FromName { get; set; } = "Schichtplaner";

	public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(FromAddress);
}
