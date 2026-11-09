using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Muddi.ShiftPlanner.Server.Api.Services.Promotion;
using Muddi.ShiftPlanner.Server.Database.Contexts;
using Muddi.ShiftPlanner.Server.Database.Entities;

namespace Muddi.ShiftPlanner.Server.Api.Endpoints.Promotions;

public class GetScheduleEndpoint : CrudEndpoint<SeasonPromotionRequest, PromotionScheduleResponse>
{
	private readonly PromotionService _promotions;

	public GetScheduleEndpoint(ShiftPlannerContext database, PromotionService promotions) : base(database)
	{
		_promotions = promotions;
	}

	protected override void CrudConfigure()
	{
		Get("/seasons/{SeasonId}/promotions/schedule");
	}

	public override async Task<PromotionScheduleResponse> ExecuteAsync(SeasonPromotionRequest req, CancellationToken ct)
	{
		var season = await Database.Seasons.FindAsync([req.SeasonId], ct);
		if (season is null)
			ThrowError("Season not found", 404);

		var schedule = await _promotions.GetScheduleAsync(season.Id, ct);
		var clock = await _promotions.GetClockAsync(schedule, ct);
		var nextRuns = schedule.Enabled
			? PromotionPlanner.NextRuns(schedule, PromotionClock.SeasonDate(season.StartDate),
					PromotionClock.SeasonDate(season.EndDate), clock, _promotions.UtcNow, 3)
				.Select(r => new PromotionNextRunResponse { Kind = r.Kind, SendAt = r.SendAtUtc })
				.ToList()
			: [];
		return new PromotionScheduleResponse
		{
			Schedule = schedule,
			NextRuns = nextRuns,
			EnabledChannels = await Database.PromotionChannels.CountAsync(c => c.Season.Id == season.Id && c.Enabled, ct)
		};
	}
}

public class UpdateScheduleEndpoint : CrudEndpoint<UpdatePromotionScheduleRequest, EmptyResponse>
{
	public UpdateScheduleEndpoint(ShiftPlannerContext database) : base(database)
	{
	}

	protected override void CrudConfigure()
	{
		Put("/seasons/{SeasonId}/promotions/schedule");
	}

	public override async Task<EmptyResponse> ExecuteAsync(UpdatePromotionScheduleRequest req, CancellationToken ct)
	{
		var season = await Database.Seasons.FindAsync([req.SeasonId], ct);
		if (season is null)
			ThrowError("Season not found", 404);

		var s = req.Schedule;
		if (PromotionClock.TryFindTimeZone(s.TimeZone) is null)
			ThrowError($"Unbekannte Zeitzone \"{s.TimeZone}\"");
		if (!string.IsNullOrWhiteSpace(s.PublicBaseUrl) &&
		    !(Uri.TryCreate(s.PublicBaseUrl, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"))
			ThrowError("Die Adresse der Web-App muss mit http:// oder https:// beginnen");
		if (s.Enabled && string.IsNullOrWhiteSpace(s.PublicBaseUrl))
			ThrowError("Die Adresse der Web-App wird für die Links in den Nachrichten benötigt");

		var schedule = s with
		{
			PublicBaseUrl = s.PublicBaseUrl.Trim().TrimEnd('/'),
			MinOpenShifts = Math.Clamp(s.MinOpenShifts, 1, 1000),
			DailyLookaheadDays = Math.Clamp(s.DailyLookaheadDays, 1, 14),
			LastCallHoursBefore = Math.Clamp(s.LastCallHoursBefore, 1, 48),
			DailySendTime = ClampTime(s.DailySendTime),
			RecruitingSendTime = ClampTime(s.RecruitingSendTime),
			QuietFrom = ClampTime(s.QuietFrom),
			QuietUntil = ClampTime(s.QuietUntil)
		};

		var entity = await Database.PromotionSchedules.FirstOrDefaultAsync(x => x.Season.Id == season.Id, ct);
		if (entity is null)
			Database.PromotionSchedules.Add(new PromotionScheduleEntity { Season = season, Settings = schedule });
		else
			entity.Settings = schedule;
		await Database.SaveChangesAsync(ct);
		return EmptyResponse.Instance;
	}

	private static TimeSpan ClampTime(TimeSpan time) =>
		time < TimeSpan.Zero || time >= TimeSpan.FromDays(1) ? TimeSpan.Zero : time;
}
