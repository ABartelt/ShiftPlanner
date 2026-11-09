using Microsoft.EntityFrameworkCore;
using Muddi.ShiftPlanner.Server.Database.Contexts;
using Muddi.ShiftPlanner.Shared.Entities;

namespace Muddi.ShiftPlanner.Server.Api.Services.Promotion;

/// <summary>
/// Posts open shifts of all running or upcoming seasons according to their schedules. Checks once a minute.
/// </summary>
public class PromotionScheduler(
	IServiceScopeFactory scopes,
	IHostApplicationLifetime lifetime,
	ILogger<PromotionScheduler> logger) : BackgroundService
{
	/// <summary>
	/// Postgres advisory lock, so only one API instance posts when several are running.
	/// </summary>
	private const long LockKey = 0x5348494654_50524F; // "SHIFT" "PRO"

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		// Wait for the database migrations, which run while the app starts.
		var started = new TaskCompletionSource();
		await using (lifetime.ApplicationStarted.Register(() => started.TrySetResult()))
		await using (stoppingToken.Register(() => started.TrySetResult()))
			await started.Task;
		if (stoppingToken.IsCancellationRequested)
			return;

		using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
		do
		{
			try
			{
				await TickAsync(stoppingToken);
			}
			catch (Exception e) when (!stoppingToken.IsCancellationRequested)
			{
				logger.LogError(e, "Promotion scheduler run failed");
			}
		} while (await timer.WaitForNextTickAsync(stoppingToken));
	}

	private async Task TickAsync(CancellationToken ct)
	{
		await using var scope = scopes.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<ShiftPlannerContext>();
		var promotions = scope.ServiceProvider.GetRequiredService<PromotionService>();

		await database.Database.OpenConnectionAsync(ct);
		try
		{
			var locked = await database.Database
				.SqlQuery<bool>($"SELECT pg_try_advisory_lock({LockKey}) AS \"Value\"")
				.SingleAsync(ct);
			if (!locked)
				return;
			try
			{
				await RunDueAsync(database, promotions, ct);
			}
			finally
			{
				await database.Database.ExecuteSqlAsync($"SELECT pg_advisory_unlock({LockKey})", ct);
			}
		}
		finally
		{
			await database.Database.CloseConnectionAsync();
		}
	}

	private async Task RunDueAsync(ShiftPlannerContext database, PromotionService promotions, CancellationToken ct)
	{
		var now = promotions.UtcNow;
		var schedules = await database.PromotionSchedules
			.Include(s => s.Season)
			.Where(s => s.Season.EndDate >= now.AddDays(-2))
			.AsNoTracking()
			.ToListAsync(ct);

		foreach (var schedule in schedules.Where(s => s.Settings.Enabled))
		{
			var season = schedule.Season;
			var clock = await promotions.GetClockAsync(schedule.Settings, ct);
			var runs = PromotionPlanner.DueRuns(schedule.Settings, PromotionClock.SeasonDate(season.StartDate),
				PromotionClock.SeasonDate(season.EndDate), clock, now).ToList();
			if (runs.Count == 0)
				continue;

			var channels = await database.PromotionChannels
				.Include(c => c.Season)
				.Where(c => c.Season.Id == season.Id && c.Enabled)
				.ToListAsync(ct);
			foreach (var run in runs)
			foreach (var channel in channels)
			{
				// Each run is attempted once per channel; failures show up in the log and can be resent manually.
				if (await database.PromotionLogs.AnyAsync(l => l.Channel!.Id == channel.Id && l.RunKey == run.RunKey, ct))
					continue;

				var digest = await promotions.BuildDigestAsync(channel.Season, run.Kind, run.FromUtc, run.ToUtc, clock,
					schedule.Settings.PublicBaseUrl, channel.Config.LocationIds, ct);
				if (digest.TotalOpen < Math.Max(1, schedule.Settings.MinOpenShifts))
				{
					// Last calls are checked every hour; logging every empty one would flood the log.
					if (run.Kind != PromotionRunKind.LastCall)
						await promotions.LogSkippedAsync(channel, run, digest.TotalOpen,
							$"Nur {digest.TotalOpen} freie Schichten", ct);
					continue;
				}

				var log = await promotions.SendAsync(channel, digest, run.RunKey, ct);
				logger.LogInformation("Promotion {RunKey} to channel {Channel}: {Status}", run.RunKey, channel.Id, log.Status);
			}
		}
	}
}
