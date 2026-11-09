using System.Globalization;
using System.Net;
using System.Text;
using Muddi.ShiftPlanner.Services.Alerting.Inbound;
using Muddi.ShiftPlanner.Shared.Contracts.v1;

namespace Muddi.ShiftPlanner.Services.Alerting.Notifications;

/// <summary>
/// German texts of reminders and bot replies. Pure, so they can be unit tested.
/// </summary>
public static class ReminderTexts
{
	// Alpine images run in invariant globalization mode, so German names can't come from CultureInfo
	private static readonly string[] WeekDays = ["So", "Mo", "Di", "Mi", "Do", "Fr", "Sa"];

	public static string Day(DateTime local)
		=> $"{WeekDays[(int)local.DayOfWeek]}, {local.ToString("dd.MM.", CultureInfo.InvariantCulture)}";

	public static string Time(DateTime local) => local.ToString("HH:mm", CultureInfo.InvariantCulture);

	public static string Range(DateTime startLocal, DateTime endLocal)
		=> $"{Day(startLocal)} {Time(startLocal)}–{Time(endLocal)}";

	public static string Range(ShiftSummary s) => Range(s.StartLocal, s.EndLocal);

	private static string Range(ReminderMessage m) => Range(m.StartLocal, m.EndLocal);

	private static string H(string text) => WebUtility.HtmlEncode(text);

	private static string LockedHint(string? contact)
		=> contact is null
			? "Abgeben geht jetzt nur noch über die Orga, bitte melde dich dort."
			: $"Abgeben geht jetzt nur noch über die Orga, bitte melde dich bei {contact}.";

	#region reminders

	public static string Headline(ReminderMessage m) => m.Kind == ReminderKind.DayBefore
		? $"Erinnerung: Deine Schicht am {Day(m.StartLocal)} um {Time(m.StartLocal)}"
		: $"Gleich geht's los: Deine Schicht beginnt um {Time(m.StartLocal)}";

	/// <summary>
	/// Telegram, parse mode HTML.
	/// </summary>
	public static string TelegramHtml(ReminderMessage m)
	{
		var sb = new StringBuilder();
		sb.Append("⏰ <b>").Append(H(Headline(m))).Append("</b>\n\n");
		sb.Append("<b>").Append(H(m.ShiftTypeName)).Append("</b> · ").Append(H(m.LocationName)).Append('\n');
		sb.Append(H(Range(m))).Append('\n');
		if (m.Kind == ReminderKind.DayBefore)
		{
			sb.Append('\n');
			sb.Append(m.CanRelease
				? "Kannst du doch nicht? Gib die Schicht ab, damit jemand anderes sie übernehmen kann."
				: H(LockedHint(m.AdminContact)));
		}

		return sb.ToString();
	}

	/// <summary>
	/// Signal and WhatsApp without template. <paramref name="releaseByCode"/> tells the user to reply with the action code.
	/// </summary>
	public static string PlainText(ReminderMessage m, bool releaseByCode)
	{
		var sb = new StringBuilder();
		sb.Append("⏰ ").Append(Headline(m)).Append("\n\n");
		sb.Append(m.ShiftTypeName).Append(" · ").Append(m.LocationName).Append('\n');
		sb.Append(Range(m)).Append('\n');
		if (m.Kind == ReminderKind.DayBefore)
		{
			sb.Append('\n');
			if (!m.CanRelease)
				sb.Append(LockedHint(m.AdminContact)).Append('\n');
			else if (releaseByCode && m.ActionCode is not null)
				sb.Append("Kannst du doch nicht? Antworte mit „ABGEBEN ").Append(m.ActionCode)
					.Append("“, um die Schicht abzugeben.\n");
			else
				sb.Append("Kannst du doch nicht? Antworte mit „ABGEBEN“, um die Schicht abzugeben.\n");
		}

		sb.Append('\n').Append(m.Kind == ReminderKind.DayBefore ? "Andere Schicht suchen: " : "Im Schichtplan: ")
			.Append(m.ShiftLink);
		return sb.ToString();
	}

	/// <summary>
	/// Variables of the WhatsApp content templates, see TwilioTemplateOptions.
	/// </summary>
	public static Dictionary<string, string> WhatsAppVariables(ReminderMessage m)
	{
		var variables = new Dictionary<string, string>
		{
			["1"] = m.ShiftTypeName,
			["2"] = m.LocationName,
			["3"] = Range(m),
			["4"] = m.ShiftLink
		};
		if (m.Kind == ReminderKind.DayBefore)
			variables["5"] = InboundPayloads.Release(m.NotificationId);
		return variables;
	}

	public static string EmailSubject(ReminderMessage m) => $"{Headline(m)} – {m.ShiftTypeName}";

	public static string EmailText(ReminderMessage m)
	{
		var sb = new StringBuilder();
		sb.Append(Headline(m)).Append("\n\n");
		sb.Append(m.ShiftTypeName).Append(" · ").Append(m.LocationName).Append('\n');
		sb.Append(Range(m)).Append("\n\n");
		if (m.Kind == ReminderKind.DayBefore)
		{
			if (m.ReleaseUrl is not null)
				sb.Append("Kannst du doch nicht? Hier kannst du die Schicht abgeben: ").Append(m.ReleaseUrl).Append('\n');
			else if (!m.CanRelease)
				sb.Append(LockedHint(m.AdminContact)).Append('\n');
		}

		sb.Append(m.Kind == ReminderKind.DayBefore ? "Andere Schicht suchen: " : "Im Schichtplan: ")
			.Append(m.ShiftLink).Append('\n');
		if (m.UnsubscribeUrl is not null)
			sb.Append("\nKeine Erinnerungen mehr per E-Mail: ").Append(m.UnsubscribeUrl).Append('\n');
		return sb.ToString();
	}

	public static string EmailHtml(ReminderMessage m)
	{
		const string button =
			"display:inline-block;padding:10px 18px;margin:4px 8px 4px 0;border-radius:6px;text-decoration:none;font-weight:600;";
		var sb = new StringBuilder();
		sb.Append("<!DOCTYPE html><html lang=\"de\"><body style=\"font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;color:#1f2937;line-height:1.5\">");
		sb.Append("<div style=\"max-width:520px;margin:0 auto;padding:16px\">");
		sb.Append("<h2 style=\"margin:0 0 16px\">⏰ ").Append(H(Headline(m))).Append("</h2>");
		sb.Append("<p style=\"margin:0 0 4px;font-size:18px\"><strong>").Append(H(m.ShiftTypeName))
			.Append("</strong> · ").Append(H(m.LocationName)).Append("</p>");
		sb.Append("<p style=\"margin:0 0 16px\">").Append(H(Range(m))).Append("</p>");
		if (m.Kind == ReminderKind.DayBefore)
		{
			if (m.ReleaseUrl is not null)
			{
				sb.Append("<p>Kannst du doch nicht? Gib die Schicht ab, damit jemand anderes sie übernehmen kann.</p>");
				sb.Append("<a href=\"").Append(H(m.ReleaseUrl)).Append("\" style=\"").Append(button)
					.Append("background:#dc2626;color:#ffffff\">Schicht abgeben</a>");
			}
			else if (!m.CanRelease)
			{
				sb.Append("<p>").Append(H(LockedHint(m.AdminContact))).Append("</p>");
			}
		}

		sb.Append("<a href=\"").Append(H(m.ShiftLink)).Append("\" style=\"").Append(button)
			.Append("background:#e5e7eb;color:#111827\">")
			.Append(m.Kind == ReminderKind.DayBefore ? "Andere Schicht suchen" : "Im Schichtplan öffnen").Append("</a>");
		if (m.UnsubscribeUrl is not null)
			sb.Append("<p style=\"margin-top:32px;font-size:12px;color:#6b7280\"><a href=\"").Append(H(m.UnsubscribeUrl))
				.Append("\" style=\"color:#6b7280\">Keine Erinnerungen mehr per E-Mail</a></p>");
		sb.Append("</div></body></html>");
		return sb.ToString();
	}

	#endregion

	#region replies

	public const string Linked =
		"✅ Verknüpft! Du bekommst ab jetzt 24 Stunden und 30 Minuten vor deinen Schichten eine Erinnerung. Mit STOP bestellst du sie ab.";

	public const string LinkFailed =
		"❌ Der Code ist ungültig oder abgelaufen. Erzeuge in der Schichtplaner-App einen neuen.";

	public const string Stopped =
		"🔕 Erinnerungen abbestellt. Du kannst sie in der Schichtplaner-App wieder aktivieren.";

	public const string NotLinked =
		"Dieses Konto ist mit keinem Schichtplaner-Konto verknüpft. Verknüpfe es in der Schichtplaner-App unter Benachrichtigungen.";

	public const string NothingToRelease = "Es gibt gerade keine Schicht, die du hier abgeben kannst.";

	public const string Kept = "👍 Alles klar, die Schicht bleibt bei dir.";

	public static string Help(NotificationChannel channel)
	{
		var sb = new StringBuilder(
			"Hallo! Ich erinnere dich an deine Schichten. Verknüpfe dazu dein Konto in der Schichtplaner-App unter Benachrichtigungen.\n\nBefehle:\n");
		if (channel != NotificationChannel.Telegram)
			sb.Append("LINK <Code> – Konto verknüpfen\n");
		sb.Append(channel == NotificationChannel.Signal
			? "ABGEBEN <Code> – Schicht aus der Erinnerung abgeben\n"
			: "ABGEBEN – Schicht aus der letzten Erinnerung abgeben\n");
		sb.Append("STOP – Erinnerungen abbestellen");
		return sb.ToString();
	}

	public static string ConfirmRelease(ShiftSummary s, bool withButtons)
	{
		var question = $"Willst du die Schicht {s.ShiftTypeName} · {s.LocationName} am {Range(s)} wirklich abgeben?";
		return withButtons ? question : question + " Antworte mit JA oder NEIN.";
	}

	public static string ReleaseResult(ReleaseResult result) => result.Outcome switch
	{
		ReleaseOutcome.Released =>
			$"✅ Die Schicht wurde abgegeben, danke für deine Rückmeldung! Eine andere freie Schicht findest du hier: {result.Summary!.ShiftLink}",
		ReleaseOutcome.AlreadyReleased => "Diese Schicht hast du bereits abgegeben.",
		ReleaseOutcome.Locked => "🔒 Die Schicht kann so kurzfristig nicht mehr abgegeben werden. " + LockedHint(result.AdminContact),
		_ => "Diese Schicht gibt es nicht mehr oder sie ist nicht mehr dir zugeordnet."
	};

	#endregion
}
