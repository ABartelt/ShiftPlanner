namespace Muddi.ShiftPlanner.Shared.Contracts.v1.Responses;

public class CreateChannelLinkResponse
{
	public NotificationChannel Channel { get; set; }

	/// <summary>
	/// True when the channel is linked right away (e-mail); otherwise the user has to send <see cref="Code"/> to the bot.
	/// </summary>
	public bool Linked { get; set; }

	public string? Code { get; set; }
	public DateTime? ExpiresAt { get; set; }

	/// <summary>
	/// Opens the messenger with the bot (and for Telegram/WhatsApp the code already filled in).
	/// </summary>
	public string? DeepLink { get; set; }

	public string Instructions { get; set; } = string.Empty;
}
