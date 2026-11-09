using Microsoft.EntityFrameworkCore;
using Muddi.ShiftPlanner.Server.Database.Contexts;
using Muddi.ShiftPlanner.Server.Database.Entities;
using Muddi.ShiftPlanner.Shared.Entities;

namespace Muddi.ShiftPlanner.Server.Api.Services.Promotion;

/// <summary>
/// Builds digests from the database and sends them to channels, logging every attempt.
/// </summary>
public class PromotionService(
	ShiftPlannerContext database,
	PromotionSecrets secrets,
	PromotionSenders senders,
	TimeProvider time,
	ILogger<PromotionService> logger)
{
	public DateTime UtcNow => time.GetUtcNow().UtcDateTime;

	public async Task<PromotionSchedule> GetScheduleAsync(Guid seasonId, CancellationToken ct)
	{
		var entity = await database.PromotionSchedules.AsNoTracking()
			.FirstOrDefaultAsync(s => s.Season.Id == seasonId, ct);
		return entity?.Settings ?? new PromotionSchedule();
	}

	public async Task<PromotionClock> GetClockAsync(PromotionSchedule schedule, CancellationToken ct)
	{
		// Event days start with the day start of the planner, so late-night shifts belong to the evening before.
		var settings = await database.AppSettings.AsNoTracking().OrderBy(s => s.Id).FirstOrDefaultAsync(ct);
		return new PromotionClock(schedule.TimeZone, settings?.Settings.StartTime ?? TimeSpan.Zero);
	}

	/// <summary>
	/// Open shifts of a season starting in [fromUtc, toUtc), never in the past.
	/// </summary>
	public async Task<PromotionDigest> BuildDigestAsync(SeasonEntity season, PromotionRunKind kind, DateTime fromUtc,
		DateTime toUtc, PromotionClock clock, string baseUrl, IReadOnlyCollection<Guid> locationIds, CancellationToken ct)
	{
		if (fromUtc < UtcNow)
			fromUtc = UtcNow;
		var locations = await database.ShiftLocations
			.Where(l => l.Season.Id == season.Id)
			.Include(l => l.Type)
			.Include(l => l.Containers.Where(c => c.End > fromUtc && c.Start < toUtc))
			.ThenInclude(c => c.Framework)
			.ThenInclude(f => f.ShiftTypeCounts)
			.ThenInclude(stc => stc.ShiftType)
			.Include(l => l.Containers.Where(c => c.End > fromUtc && c.Start < toUtc))
			.ThenInclude(c => c.Shifts)
			.ThenInclude(s => s.Type)
			.AsSplitQuery()
			.AsNoTracking()
			.ToListAsync(ct);
		return PromotionDigestBuilder.Build(season.Name, kind, locations, fromUtc, toUtc, clock, baseUrl, locationIds);
	}

	/// <summary>
	/// Sends a digest to a channel and logs the result. Never throws for messenger errors.
	/// </summary>
	public async Task<PromotionLogEntity> SendAsync(PromotionChannelEntity channel, PromotionDigest digest,
		string runKey, CancellationToken ct)
	{
		var log = new PromotionLogEntity
		{
			Season = channel.Season,
			Channel = channel,
			ChannelName = channel.Name,
			RunKind = digest.Kind,
			RunKey = runKey,
			SentAt = UtcNow,
			OpenShifts = digest.TotalOpen,
			Status = PromotionLogStatus.Sent
		};
		try
		{
			var text = PromotionFormatter.Format(digest, PromotionSenders.FormatOf(channel.Kind));
			await senders.SendAsync(channel.Kind, channel.Config, secrets.Unprotect(channel.EncryptedSecret), text,
				digest.SignUpUrl, ct);
		}
		catch (PromotionSendException e)
		{
			logger.LogWarning("Promotion to channel {Channel} failed: {Error}", channel.Id, e.Message);
			log.Status = PromotionLogStatus.Failed;
			log.Error = Truncate(e.Message, 500);
		}

		database.PromotionLogs.Add(log);
		await database.SaveChangesAsync(ct);
		return log;
	}

	/// <summary>
	/// Records that a scheduled run was not posted (e.g. too few open shifts).
	/// </summary>
	public async Task LogSkippedAsync(PromotionChannelEntity channel, PromotionRun run, int openShifts, string reason,
		CancellationToken ct)
	{
		database.PromotionLogs.Add(new PromotionLogEntity
		{
			Season = channel.Season,
			Channel = channel,
			ChannelName = channel.Name,
			RunKind = run.Kind,
			RunKey = run.RunKey,
			SentAt = UtcNow,
			OpenShifts = openShifts,
			Status = PromotionLogStatus.Skipped,
			Error = reason
		});
		await database.SaveChangesAsync(ct);
	}

	/// <summary>
	/// Sends a test message and stores the result on the channel.
	/// </summary>
	public async Task<(bool Ok, string Message)> TestAsync(PromotionChannelEntity channel, CancellationToken ct)
	{
		var format = PromotionSenders.FormatOf(channel.Kind);
		(bool Ok, string Message) result;
		try
		{
			await senders.SendAsync(channel.Kind, channel.Config, secrets.Unprotect(channel.EncryptedSecret),
				PromotionFormatter.TestMessage(format), (await GetScheduleAsync(channel.Season.Id, ct)).PublicBaseUrl, ct);
			result = (true, "Testnachricht gesendet");
		}
		catch (PromotionSendException e)
		{
			result = (false, e.Message);
		}

		channel.LastTestAt = UtcNow;
		channel.LastTestOk = result.Ok;
		channel.LastTestMessage = Truncate(result.Message, 500);
		await database.SaveChangesAsync(ct);
		return result;
	}

	private static string Truncate(string text, int length) => text.Length <= length ? text : text[..length];
}
