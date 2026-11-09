namespace Muddi.ShiftPlanner.Services.Alerting.Inbound;

public enum InboundCommandType
{
	Unknown,
	Help,

	/// <summary>
	/// "/start CODE" (Telegram deep link) or "LINK CODE".
	/// </summary>
	Link,

	/// <summary>
	/// Release button or "ABGEBEN"; asks for confirmation.
	/// </summary>
	ReleaseRequest,

	/// <summary>
	/// Telegram confirmation button.
	/// </summary>
	ReleaseConfirm,

	/// <summary>
	/// Telegram cancel button.
	/// </summary>
	ReleaseCancel,

	/// <summary>
	/// "ABGEBEN 123456", releases without asking again.
	/// </summary>
	ReleaseByCode,
	Yes,
	No,
	Stop
}

public readonly record struct InboundCommand(InboundCommandType Type, string? Argument = null, Guid? NotificationId = null);

/// <summary>
/// Callback data of the Telegram buttons and quick-reply ids of the WhatsApp template (at most 64 bytes).
/// </summary>
public static class InboundPayloads
{
	public static string Release(Guid notificationId) => $"rel:{notificationId:N}";
	public static string Confirm(Guid notificationId) => $"relok:{notificationId:N}";
	public static string Cancel(Guid notificationId) => $"relno:{notificationId:N}";
}

public static class InboundCommandParser
{
	public static InboundCommand Parse(string? text, string? payload = null)
	{
		if (!string.IsNullOrWhiteSpace(payload) && ParsePayload(payload.Trim()) is { } fromPayload)
			return fromPayload;

		var parts = (text ?? string.Empty).Split(' ', 2,
			StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (parts.Length == 0)
			return new(InboundCommandType.Unknown);

		var head = parts[0].ToUpperInvariant();
		var argument = parts.Length > 1 ? parts[1] : null;
		// Telegram adds the bot name in groups: /start@MuddiBot
		var at = head.IndexOf('@');
		if (head.StartsWith('/') && at > 0)
			head = head[..at];

		switch (head)
		{
			case "/START" or "LINK":
				return argument is null
					? new(InboundCommandType.Help)
					: new(InboundCommandType.Link, NormalizeCode(argument));
			case "ABGEBEN" or "/ABGEBEN":
				var code = argument?.Replace(" ", string.Empty);
				return code is { Length: 6 } && code.All(char.IsAsciiDigit)
					? new(InboundCommandType.ReleaseByCode, code)
					: new(InboundCommandType.ReleaseRequest);
			// Text of the WhatsApp quick-reply button
			case "SCHICHT" when argument?.Equals("abgeben", StringComparison.OrdinalIgnoreCase) == true:
				return new(InboundCommandType.ReleaseRequest);
			case "JA" or "YES":
				return new(InboundCommandType.Yes);
			case "NEIN" or "NO":
				return new(InboundCommandType.No);
			case "STOP" or "STOPP" or "/STOP":
				return new(InboundCommandType.Stop);
			case "HILFE" or "HELP" or "/HELP" or "/HILFE":
				return new(InboundCommandType.Help);
			default:
				return new(InboundCommandType.Unknown);
		}
	}

	private static InboundCommand? ParsePayload(string payload)
	{
		var colon = payload.IndexOf(':');
		if (colon < 0)
			return payload.Equals("abgeben", StringComparison.OrdinalIgnoreCase)
			       || payload.Equals("release", StringComparison.OrdinalIgnoreCase)
				? new InboundCommand(InboundCommandType.ReleaseRequest)
				: null;

		if (!Guid.TryParseExact(payload[(colon + 1)..], "N", out var id))
			return null;
		return payload[..colon] switch
		{
			"rel" => new InboundCommand(InboundCommandType.ReleaseRequest, NotificationId: id),
			"relok" => new InboundCommand(InboundCommandType.ReleaseConfirm, NotificationId: id),
			"relno" => new InboundCommand(InboundCommandType.ReleaseCancel, NotificationId: id),
			_ => null
		};
	}

	private static string NormalizeCode(string code) => code.Replace(" ", string.Empty).Replace("-", string.Empty)
		.ToUpperInvariant();
}
