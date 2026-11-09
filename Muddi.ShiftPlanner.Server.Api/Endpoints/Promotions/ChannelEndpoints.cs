using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Muddi.ShiftPlanner.Server.Api.Services.Promotion;
using Muddi.ShiftPlanner.Server.Database.Contexts;
using Muddi.ShiftPlanner.Server.Database.Entities;
using Muddi.ShiftPlanner.Shared.Entities;

namespace Muddi.ShiftPlanner.Server.Api.Endpoints.Promotions;

public class GetAllChannelsEndpoint : CrudGetAllEndpoint<SeasonPromotionRequest, PromotionChannelResponse>
{
	public GetAllChannelsEndpoint(ShiftPlannerContext database) : base(database)
	{
	}

	protected override void CrudConfigure()
	{
		Get("/seasons/{SeasonId}/promotions/channels");
	}

	public override async Task<List<PromotionChannelResponse>?> CrudExecuteAsync(SeasonPromotionRequest req,
		CancellationToken ct)
	{
		var channels = await Database.PromotionChannels
			.Include(c => c.Season)
			.Where(c => c.Season.Id == req.SeasonId)
			.OrderBy(c => c.Name)
			.AsNoTracking()
			.ToListAsync(ct);
		return channels.Select(c => c.ToResponse()).ToList();
	}
}

public class CreateChannelEndpoint : CrudEndpoint<CreatePromotionChannelRequest, PromotionChannelResponse>
{
	private readonly PromotionSecrets _secrets;

	public CreateChannelEndpoint(ShiftPlannerContext database, PromotionSecrets secrets) : base(database)
	{
		_secrets = secrets;
	}

	protected override void CrudConfigure()
	{
		Post("/seasons/{SeasonId}/promotions/channels");
	}

	public override async Task<PromotionChannelResponse> ExecuteAsync(CreatePromotionChannelRequest req,
		CancellationToken ct)
	{
		var season = await Database.Seasons.FindAsync([req.SeasonId], ct);
		if (season is null)
			ThrowError("Season not found", 404);

		var secret = string.IsNullOrWhiteSpace(req.Secret) ? null : req.Secret.Trim();
		if (ValidateChannel(req.Name, req.Kind, req.Config, secret) is { } error)
			ThrowError(error);

		var channel = new PromotionChannelEntity
		{
			Season = season,
			Name = req.Name.Trim(),
			Kind = req.Kind,
			Enabled = req.Enabled,
			Config = Normalize(req.Config),
			EncryptedSecret = secret is null ? null : _secrets.Protect(secret),
			SecretHint = secret is null ? null : PromotionSecrets.Hint(secret)
		};
		Database.PromotionChannels.Add(channel);
		await Database.SaveChangesAsync(ct);
		return channel.ToResponse();
	}

	/// <summary>
	/// Returns what is wrong with the channel, or null.
	/// </summary>
	internal static string? ValidateChannel(string name, PromotionChannelKind kind, PromotionChannelConfig config,
		string? secret) =>
		string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100
			? "Bitte einen Namen (max. 100 Zeichen) angeben"
			: PromotionSenders.Validate(kind, config, secret);

	internal static PromotionChannelConfig Normalize(PromotionChannelConfig config) => config with
	{
		Target = config.Target.Trim(),
		TargetName = config.TargetName.Trim(),
		SignalNumber = config.SignalNumber.Trim(),
		ServiceUrl = config.ServiceUrl.Trim().TrimEnd('/'),
		LocationIds = config.LocationIds.Distinct().ToList()
	};
}

public class UpdateChannelEndpoint : CrudEndpoint<UpdatePromotionChannelRequest, EmptyResponse>
{
	private readonly PromotionSecrets _secrets;

	public UpdateChannelEndpoint(ShiftPlannerContext database, PromotionSecrets secrets) : base(database)
	{
		_secrets = secrets;
	}

	protected override void CrudConfigure()
	{
		Put("/promotions/channels/{Id}");
	}

	public override async Task<EmptyResponse> ExecuteAsync(UpdatePromotionChannelRequest req, CancellationToken ct)
	{
		var channel = await Database.PromotionChannels.FirstOrDefaultAsync(c => c.Id == req.Id, ct);
		if (channel is null)
			ThrowError("Channel not found", 404);

		// null keeps the stored secret, "" removes it
		var secret = req.Secret is null
			? _secrets.Unprotect(channel.EncryptedSecret)
			: string.IsNullOrWhiteSpace(req.Secret) ? null : req.Secret.Trim();
		if (CreateChannelEndpoint.ValidateChannel(req.Name, channel.Kind, req.Config, secret) is { } error)
			ThrowError(error);

		channel.Name = req.Name.Trim();
		channel.Enabled = req.Enabled;
		channel.Config = CreateChannelEndpoint.Normalize(req.Config);
		if (req.Secret is not null)
		{
			channel.EncryptedSecret = secret is null ? null : _secrets.Protect(secret);
			channel.SecretHint = secret is null ? null : PromotionSecrets.Hint(secret);
		}

		await Database.SaveChangesAsync(ct);
		return EmptyResponse.Instance;
	}
}

public class DeleteChannelEndpoint : CrudDeleteEndpoint
{
	public DeleteChannelEndpoint(ShiftPlannerContext database) : base(database)
	{
	}

	protected override void CrudConfigure()
	{
		Delete("/promotions/channels/{Id}");
	}

	protected override async Task<DeleteResponse> CrudExecuteAsync(Guid id, CancellationToken ct)
	{
		var channel = await Database.PromotionChannels.FindAsync([id], ct);
		if (channel is null)
			return DeleteResponse.NotFound;
		Database.PromotionChannels.Remove(channel);
		await Database.SaveChangesAsync(ct);
		return DeleteResponse.OK;
	}
}

public class TestChannelEndpoint : CrudEndpoint<DefaultGetRequest, PromotionTestResponse>
{
	private readonly PromotionService _promotions;

	public TestChannelEndpoint(ShiftPlannerContext database, PromotionService promotions) : base(database)
	{
		_promotions = promotions;
	}

	protected override void CrudConfigure()
	{
		Post("/promotions/channels/{Id}/test");
	}

	public override async Task<PromotionTestResponse> ExecuteAsync(DefaultGetRequest req, CancellationToken ct)
	{
		var channel = await Database.PromotionChannels
			.Include(c => c.Season)
			.FirstOrDefaultAsync(c => c.Id == req.Id, ct);
		if (channel is null)
			ThrowError("Channel not found", 404);

		var (ok, message) = await _promotions.TestAsync(channel, ct);
		return new PromotionTestResponse { Ok = ok, Message = message };
	}
}
