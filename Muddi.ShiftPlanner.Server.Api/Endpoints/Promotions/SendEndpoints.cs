using Microsoft.EntityFrameworkCore;
using Muddi.ShiftPlanner.Server.Api.Services.Promotion;
using Muddi.ShiftPlanner.Server.Database.Contexts;
using Muddi.ShiftPlanner.Server.Database.Entities;
using Muddi.ShiftPlanner.Shared.Entities;

namespace Muddi.ShiftPlanner.Server.Api.Endpoints.Promotions;

/// <summary>
/// Message text for manual sharing (WhatsApp, Signal, copy) and for previewing automatic posts.
/// </summary>
public class PreviewEndpoint : CrudEndpoint<GetPromotionPreviewRequest, PromotionPreviewResponse>
{
	private readonly PromotionService _promotions;

	public PreviewEndpoint(ShiftPlannerContext database, PromotionService promotions) : base(database)
	{
		_promotions = promotions;
	}

	protected override void CrudConfigure()
	{
		Get("/seasons/{SeasonId}/promotions/preview");
	}

	public override async Task<PromotionPreviewResponse> ExecuteAsync(GetPromotionPreviewRequest req,
		CancellationToken ct)
	{
		var season = await Database.Seasons.FindAsync([req.SeasonId], ct);
		if (season is null)
			ThrowError("Season not found", 404);
		if (!Enum.TryParse<PromotionFormat>(req.Format ?? nameof(PromotionFormat.Plain), true, out var format))
			ThrowError($"Unbekanntes Format \"{req.Format}\"");
		var locations = ParseLocations(req.Locations);
		if (locations is null)
			ThrowError("Ungültige Orte");
		if (!IsValidRange(req.From, req.To))
			ThrowError("Ungültiger Zeitraum");

		var digest = await BuildManualDigestAsync(_promotions, season, req.From, req.To, req.BaseUrl, locations, ct);
		return new PromotionPreviewResponse
		{
			Text = PromotionFormatter.Format(digest, format),
			TotalOpen = digest.TotalOpen,
			SignUpUrl = digest.SignUpUrl
		};
	}

	internal static bool IsValidRange(DateOnly from, DateOnly to) =>
		from != default && to >= from && to.DayNumber - from.DayNumber <= 366;

	internal static List<Guid>? ParseLocations(string? locations)
	{
		var ids = new List<Guid>();
		foreach (var part in (locations ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			if (!Guid.TryParse(part, out var id))
				return null;
			ids.Add(id);
		}

		return ids;
	}

	internal static async Task<PromotionDigest> BuildManualDigestAsync(PromotionService promotions, SeasonEntity season,
		DateOnly from, DateOnly to, string? requestBaseUrl, IReadOnlyCollection<Guid> locations, CancellationToken ct)
	{
		var schedule = await promotions.GetScheduleAsync(season.Id, ct);
		var clock = await promotions.GetClockAsync(schedule, ct);
		var baseUrl = string.IsNullOrWhiteSpace(schedule.PublicBaseUrl) ? requestBaseUrl ?? "" : schedule.PublicBaseUrl;
		return await promotions.BuildDigestAsync(season, PromotionRunKind.Manual, clock.StartOfDayUtc(from),
			clock.StartOfDayUtc(to.AddDays(1)), clock, baseUrl, locations, ct);
	}
}

/// <summary>
/// "Send now": posts the open shifts of a date range to the selected channels.
/// </summary>
public class SendEndpoint : CrudEndpoint<SendPromotionRequest, List<PromotionLogResponse>>
{
	private readonly PromotionService _promotions;

	public SendEndpoint(ShiftPlannerContext database, PromotionService promotions) : base(database)
	{
		_promotions = promotions;
	}

	protected override void CrudConfigure()
	{
		Post("/seasons/{SeasonId}/promotions/send");
	}

	public override async Task<List<PromotionLogResponse>> ExecuteAsync(SendPromotionRequest req, CancellationToken ct)
	{
		var season = await Database.Seasons.FindAsync([req.SeasonId], ct);
		if (season is null)
			ThrowError("Season not found", 404);
		if (!PreviewEndpoint.IsValidRange(req.From, req.To))
			ThrowError("Ungültiger Zeitraum");

		var channels = await Database.PromotionChannels
			.Include(c => c.Season)
			.Where(c => c.Season.Id == season.Id && req.ChannelIds.Contains(c.Id))
			.ToListAsync(ct);
		if (channels.Count == 0)
			ThrowError("Bitte mindestens einen Kanal auswählen");

		var runKey = $"manual:{Guid.NewGuid():N}";
		var results = new List<PromotionLogResponse>();
		foreach (var channel in channels)
		{
			var digest = await PreviewEndpoint.BuildManualDigestAsync(_promotions, season, req.From, req.To,
				req.BaseUrl, channel.Config.LocationIds, ct);
			results.Add((await _promotions.SendAsync(channel, digest, runKey, ct)).ToResponse());
		}

		return results;
	}
}

public class GetLogEndpoint : CrudGetAllEndpoint<SeasonPromotionRequest, PromotionLogResponse>
{
	public GetLogEndpoint(ShiftPlannerContext database) : base(database)
	{
	}

	protected override void CrudConfigure()
	{
		Get("/seasons/{SeasonId}/promotions/log");
	}

	public override async Task<List<PromotionLogResponse>?> CrudExecuteAsync(SeasonPromotionRequest req,
		CancellationToken ct)
	{
		var logs = await Database.PromotionLogs
			.Include(l => l.Channel)
			.Where(l => l.Season.Id == req.SeasonId)
			.OrderByDescending(l => l.SentAt)
			.Take(50)
			.AsNoTracking()
			.ToListAsync(ct);
		return logs.Select(l => l.ToResponse()).ToList();
	}
}
