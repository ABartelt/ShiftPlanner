using Muddi.ShiftPlanner.Server.Database.Entities;

namespace Muddi.ShiftPlanner.Server.Api.Endpoints.Promotions;

public static class PromotionMappings
{
	public static PromotionChannelResponse ToResponse(this PromotionChannelEntity channel) => new()
	{
		Id = channel.Id,
		SeasonId = channel.Season.Id,
		Name = channel.Name,
		Kind = channel.Kind,
		Enabled = channel.Enabled,
		Config = channel.Config,
		HasSecret = !string.IsNullOrEmpty(channel.EncryptedSecret),
		SecretHint = channel.SecretHint,
		LastTestAt = channel.LastTestAt,
		LastTestOk = channel.LastTestOk,
		LastTestMessage = channel.LastTestMessage
	};

	public static PromotionLogResponse ToResponse(this PromotionLogEntity log) => new()
	{
		Id = log.Id,
		ChannelId = log.Channel?.Id,
		ChannelName = log.ChannelName,
		RunKind = log.RunKind,
		SentAt = log.SentAt,
		OpenShifts = log.OpenShifts,
		Status = log.Status,
		Error = log.Error
	};
}
