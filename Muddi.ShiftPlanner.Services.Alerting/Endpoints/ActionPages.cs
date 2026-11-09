using System.Net;
using System.Text;
using Muddi.ShiftPlanner.Services.Alerting.Notifications;

namespace Muddi.ShiftPlanner.Services.Alerting.Endpoints;

/// <summary>
/// Minimal German HTML pages behind the links in reminder e-mails.
/// </summary>
public static class ActionPages
{
	private static string H(string text) => WebUtility.HtmlEncode(text);

	private static string Page(string title, string body)
	{
		var sb = new StringBuilder();
		sb.Append("<!DOCTYPE html><html lang=\"de\"><head><meta charset=\"utf-8\">");
		sb.Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
		sb.Append("<meta name=\"robots\" content=\"noindex\">");
		sb.Append("<title>").Append(H(title)).Append("</title>");
		sb.Append("<style>body{font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;color:#1f2937;");
		sb.Append("line-height:1.5;margin:0;padding:24px}main{max-width:480px;margin:0 auto}");
		sb.Append(".btn{display:inline-block;padding:10px 18px;margin:4px 8px 4px 0;border-radius:6px;border:0;");
		sb.Append("font:inherit;font-weight:600;text-decoration:none;cursor:pointer}");
		sb.Append(".danger{background:#dc2626;color:#fff}.plain{background:#e5e7eb;color:#111827}</style>");
		sb.Append("</head><body><main><h1>").Append(H(title)).Append("</h1>").Append(body).Append("</main></body></html>");
		return sb.ToString();
	}

	private static string Shift(ShiftSummary s)
		=> $"<p><strong>{H(s.ShiftTypeName)}</strong> · {H(s.LocationName)}<br>{H(ReminderTexts.Range(s))}</p>";

	private static string Link(string url, string text)
		=> $"<a class=\"btn plain\" href=\"{H(url)}\">{H(text)}</a>";

	/// <summary>
	/// Only a form: mail scanners open links, releasing needs the POST.
	/// </summary>
	public static string ConfirmRelease(ShiftSummary s)
		=> Page("Schicht abgeben?",
			Shift(s) + "<p>Wenn du die Schicht abgibst, wird sie wieder frei und jemand anderes kann sie übernehmen.</p>"
			         + "<form method=\"post\"><button class=\"btn danger\" type=\"submit\">Ja, Schicht abgeben</button>"
			         + Link(s.ShiftLink, "Abbrechen") + "</form>");

	public static string ReleaseResult(ReleaseResult result) => result.Outcome switch
	{
		ReleaseOutcome.Released => Page("Schicht abgegeben",
			Shift(result.Summary!) + "<p>Danke für deine Rückmeldung! Die Schicht ist wieder frei.</p>"
			                       + Link(result.Summary!.ShiftLink, "Andere Schicht suchen")),
		ReleaseOutcome.AlreadyReleased => Page("Bereits abgegeben",
			"<p>Diese Schicht hast du bereits abgegeben, der Link wurde schon verwendet.</p>"),
		ReleaseOutcome.Locked => Page("Abgeben nicht mehr möglich",
			Shift(result.Summary!) + "<p>Die Schicht kann so kurzfristig nicht mehr abgegeben werden. "
			                       + (result.AdminContact is null
				                       ? "Bitte melde dich bei der Orga."
				                       : $"Bitte melde dich bei der Orga: {H(result.AdminContact)}") + "</p>"),
		_ => NotFound
	};

	public static readonly string NotFound = Page("Nicht gefunden",
		"<p>Diese Schicht gibt es nicht mehr oder sie ist nicht mehr dir zugeordnet.</p>");

	public static readonly string ConfirmUnsubscribe = Page("Erinnerungen abbestellen?",
		"<p>Du bekommst dann keine Erinnerungen an deine Schichten mehr per E-Mail. In der Schichtplaner-App kannst du sie wieder aktivieren.</p>"
		+ "<form method=\"post\"><button class=\"btn danger\" type=\"submit\">Abbestellen</button></form>");

	public static readonly string Unsubscribed = Page("Abbestellt",
		"<p>Du bekommst keine Erinnerungen mehr per E-Mail. In der Schichtplaner-App kannst du sie wieder aktivieren.</p>");
}
