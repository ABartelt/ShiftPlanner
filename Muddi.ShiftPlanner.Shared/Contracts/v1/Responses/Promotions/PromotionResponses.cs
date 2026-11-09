using Muddi.ShiftPlanner.Shared.Entities;

namespace Muddi.ShiftPlanner.Shared.Contracts.v1.Responses;

public class PromotionChannelResponse : IMuddiResponse
{
	public Guid Id { get; set; }
	public Guid SeasonId { get; set; }
	public string Name { get; set; } = string.Empty;
	public PromotionChannelKind Kind { get; set; }
	public bool Enabled { get; set; }
	public PromotionChannelConfig Config { get; set; } = new();

	/// <summary>
	/// Whether a secret is stored. The secret itself is never returned.
	/// </summary>
	public bool HasSecret { get; set; }

	public string? SecretHint { get; set; }
	public DateTime? LastTestAt { get; set; }
	public bool? LastTestOk { get; set; }
	public string? LastTestMessage { get; set; }
}

public class PromotionNextRunResponse
{
	public PromotionRunKind Kind { get; set; }
	public DateTime SendAt { get; set; }
}

public class PromotionScheduleResponse
{
	public PromotionSchedule Schedule { get; set; } = new();
	public List<PromotionNextRunResponse> NextRuns { get; set; } = [];
	public int EnabledChannels { get; set; }
}

public class PromotionPreviewResponse
{
	public string Text { get; set; } = string.Empty;
	public int TotalOpen { get; set; }
	public string SignUpUrl { get; set; } = string.Empty;
}

public class PromotionLogResponse
{
	public Guid Id { get; set; }
	public Guid? ChannelId { get; set; }
	public string ChannelName { get; set; } = string.Empty;
	public PromotionRunKind RunKind { get; set; }
	public DateTime SentAt { get; set; }
	public int OpenShifts { get; set; }
	public PromotionLogStatus Status { get; set; }
	public string? Error { get; set; }
}

public class PromotionTestResponse
{
	public bool Ok { get; set; }
	public string Message { get; set; } = string.Empty;
}

public class TelegramChatResponse
{
	public string Id { get; set; } = string.Empty;
	public string Title { get; set; } = string.Empty;
	public string Type { get; set; } = string.Empty;
}

public class SignalGroupResponse
{
	public string Id { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
}
