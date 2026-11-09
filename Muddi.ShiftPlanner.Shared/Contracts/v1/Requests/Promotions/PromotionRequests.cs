using Muddi.ShiftPlanner.Shared.Entities;

namespace Muddi.ShiftPlanner.Shared.Contracts.v1.Requests;

public class SeasonPromotionRequest
{
	public Guid SeasonId { get; set; }
}

public class CreatePromotionChannelRequest
{
	public Guid SeasonId { get; set; }
	public string Name { get; set; } = string.Empty;
	public PromotionChannelKind Kind { get; set; }
	public bool Enabled { get; set; }
	public PromotionChannelConfig Config { get; set; } = new();

	/// <summary>
	/// Bot token (Telegram) or webhook URL (Slack).
	/// </summary>
	public string? Secret { get; set; }
}

public class UpdatePromotionChannelRequest
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public bool Enabled { get; set; }
	public PromotionChannelConfig Config { get; set; } = new();

	/// <summary>
	/// null keeps the stored secret, an empty string removes it, anything else replaces it.
	/// </summary>
	public string? Secret { get; set; }
}

public class UpdatePromotionScheduleRequest
{
	public Guid SeasonId { get; set; }
	public PromotionSchedule Schedule { get; set; } = new();
}

public class GetPromotionPreviewRequest
{
	public Guid SeasonId { get; set; }

	/// <summary>
	/// First event day (yyyy-MM-dd).
	/// </summary>
	public DateOnly From { get; set; }

	/// <summary>
	/// Last event day (yyyy-MM-dd), inclusive.
	/// </summary>
	public DateOnly To { get; set; }

	/// <summary>
	/// <see cref="PromotionFormat"/>, case-insensitive. Defaults to plain text.
	/// </summary>
	public string? Format { get; set; }

	/// <summary>
	/// Comma-separated location ids; empty means all.
	/// </summary>
	public string? Locations { get; set; }

	/// <summary>
	/// Public URL of the web app, used when the schedule has none yet.
	/// </summary>
	public string? BaseUrl { get; set; }
}

public class SendPromotionRequest
{
	public Guid SeasonId { get; set; }
	public List<Guid> ChannelIds { get; set; } = [];
	public DateOnly From { get; set; }
	public DateOnly To { get; set; }
	public string? BaseUrl { get; set; }
}

public class GetTelegramChatsRequest
{
	/// <summary>
	/// Token entered in the dialog; falls back to the stored token of <see cref="ChannelId"/>.
	/// </summary>
	public string? Token { get; set; }

	public Guid? ChannelId { get; set; }
}

public class SignalServiceRequest
{
	public string? ServiceUrl { get; set; }
	public string? Number { get; set; }
}
