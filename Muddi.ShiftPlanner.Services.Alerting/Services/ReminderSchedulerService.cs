using Microsoft.Extensions.Options;
using Muddi.ShiftPlanner.Services.Alerting.Notifications;
using Muddi.ShiftPlanner.Services.Alerting.Options;

namespace Muddi.ShiftPlanner.Services.Alerting.Services;

/// <summary>
/// Checks for due reminders every <see cref="NotificationOptions.PollInterval"/>.
/// </summary>
public class ReminderSchedulerService : BackgroundService
{
	private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);
	private readonly IServiceScopeFactory _scopes;
	private readonly NotificationOptions _options;
	private readonly TimeProvider _time;
	private readonly ILogger<ReminderSchedulerService> _logger;
	private DateTimeOffset _lastCleanup = DateTimeOffset.MinValue;

	public ReminderSchedulerService(IServiceScopeFactory scopes, IOptions<NotificationOptions> options,
		TimeProvider time, ILogger<ReminderSchedulerService> logger)
	{
		_scopes = scopes;
		_options = options.Value;
		_time = time;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		using var timer = new PeriodicTimer(_options.PollInterval, _time);
		try
		{
			do
			{
				await TickAsync(stoppingToken);
			} while (await timer.WaitForNextTickAsync(stoppingToken));
		}
		catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
		{
		}
	}

	private async Task TickAsync(CancellationToken ct)
	{
		try
		{
			await using var scope = _scopes.CreateAsyncScope();
			var dispatcher = scope.ServiceProvider.GetRequiredService<ReminderDispatcher>();
			await dispatcher.SendDueRemindersAsync(ct);
			if (_time.GetUtcNow() - _lastCleanup > CleanupInterval)
			{
				await dispatcher.CleanupAsync(ct);
				_lastCleanup = _time.GetUtcNow();
			}
		}
		catch (Exception e) when (e is not OperationCanceledException)
		{
			_logger.LogError(e, "Sending reminders failed");
		}
	}
}
