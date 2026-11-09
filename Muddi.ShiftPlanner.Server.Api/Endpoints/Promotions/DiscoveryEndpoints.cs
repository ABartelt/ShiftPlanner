using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Muddi.ShiftPlanner.Server.Api.Services.Promotion;
using Muddi.ShiftPlanner.Server.Database.Contexts;

namespace Muddi.ShiftPlanner.Server.Api.Endpoints.Promotions;

// Helpers for the channel dialog, so admins pick chats and groups instead of pasting ids.

public class GetTelegramChatsEndpoint : CrudEndpoint<GetTelegramChatsRequest, List<TelegramChatResponse>>
{
	private readonly PromotionSenders _senders;
	private readonly PromotionSecrets _secrets;

	public GetTelegramChatsEndpoint(ShiftPlannerContext database, PromotionSenders senders, PromotionSecrets secrets)
		: base(database)
	{
		_senders = senders;
		_secrets = secrets;
	}

	protected override void CrudConfigure()
	{
		Post("/promotions/telegram/chats");
	}

	public override async Task<List<TelegramChatResponse>> ExecuteAsync(GetTelegramChatsRequest req,
		CancellationToken ct)
	{
		var token = req.Token?.Trim();
		if (string.IsNullOrEmpty(token) && req.ChannelId is { } channelId)
		{
			var channel = await Database.PromotionChannels.AsNoTracking().FirstOrDefaultAsync(c => c.Id == channelId, ct);
			token = _secrets.Unprotect(channel?.EncryptedSecret);
		}

		if (string.IsNullOrEmpty(token))
			ThrowError("Bitte zuerst den Bot-Token eingeben");
		try
		{
			var chats = await _senders.GetTelegramChatsAsync(token, ct);
			return chats.Select(c => new TelegramChatResponse { Id = c.Id, Title = c.Title, Type = c.Type }).ToList();
		}
		catch (PromotionSendException e)
		{
			ThrowError(e.Message);
			throw;
		}
	}
}

public class GetSignalAccountsEndpoint : CrudEndpoint<SignalServiceRequest, List<string>>
{
	private readonly PromotionSenders _senders;

	public GetSignalAccountsEndpoint(ShiftPlannerContext database, PromotionSenders senders) : base(database)
	{
		_senders = senders;
	}

	protected override void CrudConfigure()
	{
		Get("/promotions/signal/accounts");
	}

	public override async Task<List<string>> ExecuteAsync(SignalServiceRequest req, CancellationToken ct)
	{
		try
		{
			return await _senders.GetSignalAccountsAsync(req.ServiceUrl, ct);
		}
		catch (PromotionSendException e)
		{
			ThrowError(e.Message);
			throw;
		}
	}
}

public class GetSignalGroupsEndpoint : CrudEndpoint<SignalServiceRequest, List<SignalGroupResponse>>
{
	private readonly PromotionSenders _senders;

	public GetSignalGroupsEndpoint(ShiftPlannerContext database, PromotionSenders senders) : base(database)
	{
		_senders = senders;
	}

	protected override void CrudConfigure()
	{
		Get("/promotions/signal/groups");
	}

	public override async Task<List<SignalGroupResponse>> ExecuteAsync(SignalServiceRequest req, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(req.Number))
			ThrowError("Bitte zuerst die Signal-Nummer auswählen");
		try
		{
			var groups = await _senders.GetSignalGroupsAsync(req.ServiceUrl, req.Number, ct);
			return groups.Select(g => new SignalGroupResponse { Id = g.Id, Name = g.Name }).ToList();
		}
		catch (PromotionSendException e)
		{
			ThrowError(e.Message);
			throw;
		}
	}
}

/// <summary>
/// QR code to link the Signal container as a new device: Signal app → Settings → Linked devices.
/// </summary>
public class GetSignalLinkQrEndpoint : CrudEndpoint<SignalServiceRequest, byte[]>
{
	private readonly PromotionSenders _senders;

	public GetSignalLinkQrEndpoint(ShiftPlannerContext database, PromotionSenders senders) : base(database)
	{
		_senders = senders;
	}

	protected override void CrudConfigure()
	{
		Get("/promotions/signal/link-qr");
	}

	public override async Task HandleAsync(SignalServiceRequest req, CancellationToken ct)
	{
		byte[] png;
		try
		{
			png = await _senders.GetSignalLinkQrCodeAsync(req.ServiceUrl, ct);
		}
		catch (PromotionSendException e)
		{
			ThrowError(e.Message);
			throw;
		}

		await Send.BytesAsync(png, contentType: "image/png", cancellation: ct);
	}
}
