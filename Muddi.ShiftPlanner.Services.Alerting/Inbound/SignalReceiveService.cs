using Microsoft.Extensions.Options;
using Muddi.ShiftPlanner.Services.Alerting.Channels;
using Muddi.ShiftPlanner.Services.Alerting.Options;
using Muddi.ShiftPlanner.Shared.Contracts.v1;

namespace Muddi.ShiftPlanner.Services.Alerting.Inbound;

/// <summary>
/// Polls signal-cli-rest-api for messages sent to the Signal number and answers them.
/// </summary>
public class SignalReceiveService : BackgroundService
{
	private readonly SignalChannel _signal;
	private readonly IServiceScopeFactory _scopes;
	private readonly SignalOptions _options;
	private readonly ILogger<SignalReceiveService> _logger;

	public SignalReceiveService(SignalChannel signal, IServiceScopeFactory scopes, IOptions<SignalOptions> options,
		ILogger<SignalReceiveService> logger)
	{
		_signal = signal;
		_scopes = scopes;
		_options = options.Value;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		using var timer = new PeriodicTimer(_options.ReceiveInterval);
		try
		{
			while (await timer.WaitForNextTickAsync(stoppingToken))
				await ReceiveAsync(stoppingToken);
		}
		catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
		{
		}
	}

	private async Task ReceiveAsync(CancellationToken ct)
	{
		IReadOnlyList<(string Sender, string Text)> messages;
		try
		{
			messages = await _signal.ReceiveAsync(ct);
		}
		catch (Exception e) when (e is not OperationCanceledException)
		{
			_logger.LogWarning(e, "Receiving Signal messages failed");
			return;
		}

		foreach (var (sender, text) in messages)
		{
			try
			{
				await using var scope = _scopes.CreateAsyncScope();
				var handler = scope.ServiceProvider.GetRequiredService<InboundCommandHandler>();
				var reply = await handler.HandleAsync(NotificationChannel.Signal, sender,
					InboundCommandParser.Parse(text), ct);
				await _signal.SendTextAsync(sender, reply.Text, ct);
			}
			catch (Exception e) when (e is not OperationCanceledException)
			{
				_logger.LogWarning(e, "Handling a Signal message failed");
			}
		}
	}
}
