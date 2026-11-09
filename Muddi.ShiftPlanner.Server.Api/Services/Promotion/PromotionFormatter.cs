using System.Net;
using System.Text;
using Muddi.ShiftPlanner.Shared.Entities;

namespace Muddi.ShiftPlanner.Server.Api.Services.Promotion;

/// <summary>
/// Turns a <see cref="PromotionDigest"/> into a German messenger text. Long digests are shortened
/// (per location, then a summary) so they stay below the limit of the target messenger.
/// </summary>
public static class PromotionFormatter
{
	private static readonly string[] DayNames = ["So", "Mo", "Di", "Mi", "Do", "Fr", "Sa"];

	private abstract record Style(int MaxLength)
	{
		public abstract string Bold(string text);
		public abstract string Escape(string text);

		/// <summary>
		/// Location name, linked if the messenger supports inline links; otherwise null and the URL goes on its own line.
		/// </summary>
		public virtual string? InlineLink(string text, string url) => null;
	}

	private record PlainStyle(int MaxLength) : Style(MaxLength)
	{
		public override string Bold(string text) => text;
		public override string Escape(string text) => text;
	}

	private record StarStyle(int MaxLength) : Style(MaxLength)
	{
		public override string Bold(string text) => $"*{text}*";
		public override string Escape(string text) => text;
	}

	/// <summary>
	/// signal-cli-rest-api "styled" text mode.
	/// </summary>
	private record SignalStyle() : Style(2000)
	{
		public override string Bold(string text) => $"**{text}**";
		public override string Escape(string text) => text.Replace("*", "").Replace("~", "").Replace("`", "").Replace("||", "");
	}

	private record TelegramStyle() : Style(4000)
	{
		public override string Bold(string text) => $"<b>{text}</b>";
		public override string Escape(string text) => WebUtility.HtmlEncode(text);
		public override string InlineLink(string text, string url) => $"<a href=\"{WebUtility.HtmlEncode(url)}\">{text}</a>";
	}

	private record SlackStyle() : Style(3000)
	{
		public override string Bold(string text) => $"*{text}*";
		public override string Escape(string text) => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
		public override string InlineLink(string text, string url) => $"<{url}|{text}>";
	}

	private static Style StyleOf(PromotionFormat format) => format switch
	{
		// wa.me share links get unreliable with long texts
		PromotionFormat.WhatsApp => new StarStyle(1500),
		PromotionFormat.Signal => new SignalStyle(),
		PromotionFormat.Telegram => new TelegramStyle(),
		PromotionFormat.Slack => new SlackStyle(),
		_ => new PlainStyle(4000)
	};

	public static int MaxLength(PromotionFormat format) => StyleOf(format).MaxLength;

	public static string Format(PromotionDigest digest, PromotionFormat format)
	{
		var style = StyleOf(format);
		var header = Header(digest, style);
		var footer = $"👉 {Link(style, "Jetzt eintragen", digest.SignUpUrl)}";

		// Recruiting covers the whole event, so it starts with the per-location overview.
		var bodies = digest.Kind == PromotionRunKind.Recruiting
			? new[] { ByLocation(digest, style), Summary(digest, style) }
			: new[] { ByDay(digest, style), ByLocation(digest, style), Summary(digest, style) };

		var text = "";
		foreach (var body in bodies)
		{
			text = Join(header, body, footer);
			if (text.Length <= style.MaxLength)
				return text;
		}

		return text;
	}

	public static string TestMessage(PromotionFormat format)
	{
		var style = StyleOf(format);
		return $"✅ {style.Bold("Testnachricht vom Schichtplaner")}\nDieser Kanal ist eingerichtet. Hier erscheinen ab jetzt freie Schichten.";
	}

	private static string Join(string header, string body, string footer) =>
		string.IsNullOrEmpty(body) ? $"{header}\n\n{footer}" : $"{header}\n\n{body}\n\n{footer}";

	private static string Header(PromotionDigest digest, Style style)
	{
		var season = style.Escape(digest.SeasonName);
		var total = digest.TotalOpen;
		var shifts = total == 1 ? "Schicht ist" : "Schichten sind";
		if (total == 0)
			return $"✅ {style.Bold(season)}: Im Zeitraum sind keine Schichten mehr frei.";

		return digest.Kind switch
		{
			PromotionRunKind.LastCall =>
				$"⏰ {style.Bold("Kurzfristig gesucht!")} Bei {season} {(total == 1 ? "ist" : "sind")} gleich noch {total} {(total == 1 ? "Schicht" : "Schichten")} frei.",
			PromotionRunKind.Recruiting =>
				$"📣 {style.Bold("Helfende gesucht!")} Für {season} {(total == 1 ? "ist" : "sind")} noch {total} {(total == 1 ? "Schicht" : "Schichten")} frei.",
			_ when digest.Days.Count == 1 =>
				$"📣 {style.Bold($"{season}, {DayLabel(digest.Days[0].Date)}")}: noch {total} {shifts} frei!",
			_ =>
				$"📣 {style.Bold(season)}: noch {total} {shifts} frei!"
		};
	}

	private static string ByDay(PromotionDigest digest, Style style)
	{
		var sb = new StringBuilder();
		foreach (var day in digest.Days)
		{
			if (digest.Days.Count > 1)
				sb.Append('\n').Append("📅 ").Append(style.Bold(DayLabel(day.Date))).Append('\n');
			foreach (var location in day.Locations)
				AppendLocation(sb, style, location, location.Open, Windows(location.Slots));
		}

		return sb.ToString().Trim('\n');
	}

	private static string ByLocation(PromotionDigest digest, Style style)
	{
		var sb = new StringBuilder();
		var locations = digest.Days
			.SelectMany(d => d.Locations.Select(l => (Day: d.Date, Location: l)))
			.GroupBy(x => x.Location.Id)
			.OrderBy(g => g.First().Location.Name, StringComparer.CurrentCultureIgnoreCase);
		foreach (var location in locations)
		{
			// Link to the first day with open shifts.
			var first = location.First().Location;
			var days = string.Join(", ", location.Select(x => DayLabel(x.Day)));
			AppendLocation(sb, style, first, location.Sum(x => x.Location.Open), days);
		}

		return sb.ToString().Trim('\n');
	}

	private static string Summary(PromotionDigest digest, Style style) =>
		digest.Days.Count <= 1
			? string.Empty
			: string.Join("\n", digest.Days.Select(d => $"• {DayLabel(d.Date)}: {d.Open} frei"));

	private static void AppendLocation(StringBuilder sb, Style style, PromotionLocation location, int open, string details)
	{
		var name = style.Escape(location.Name);
		var inline = style.InlineLink(style.Bold(name), location.Link);
		sb.Append("• ").Append(inline ?? style.Bold(name)).Append($": {open} frei");
		if (!string.IsNullOrEmpty(details))
			sb.Append(" – ").Append(details);
		sb.Append('\n');
		if (inline is null)
			sb.Append("   ").Append(location.Link).Append('\n');
	}

	private static string Link(Style style, string text, string url) =>
		style.InlineLink(text, url) ?? $"{text}: {url}";

	private static string Windows(IEnumerable<PromotionSlot> slots) =>
		string.Join(", ", slots.Select(s => $"{s.Start:HH:mm}–{s.End:HH:mm}" + (s.Open > 1 ? $" ({s.Open}×)" : "")));

	public static string DayLabel(DateOnly date) => $"{DayNames[(int)date.DayOfWeek]} {date:dd.MM.}";
}
