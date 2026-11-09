using Muddi.ShiftPlanner.Services.Alerting.Channels;
using Muddi.ShiftPlanner.Shared.Contracts.v1;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Muddi.ShiftPlanner.Services.Alerting.Inbound;

/// <summary>
/// Long polling, so the bot needs no public URL. Handles private messages and button presses.
/// </summary>
public class TelegramUpdateReceiver : BackgroundService, IUpdateHandler
{
	private readonly ITelegramBotClient _bot;
	private readonly TelegramChannel _channel;
	private readonly IServiceScopeFactory _scopes;
	private readonly ILogger<TelegramUpdateReceiver> _logger;

	public TelegramUpdateReceiver(ITelegramBotClient bot, TelegramChannel channel, IServiceScopeFactory scopes,
		ILogger<TelegramUpdateReceiver> logger)
	{
		_bot = bot;
		_channel = channel;
		_scopes = scopes;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var options = new ReceiverOptions { AllowedUpdates = [UpdateType.Message, UpdateType.CallbackQuery] };
		try
		{
			await _bot.ReceiveAsync(this, options, stoppingToken);
		}
		catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
		{
		}
	}

	public async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
	{
		switch (update)
		{
			case { Message: { Chat.Type: ChatType.Private, Text: { } text } message }:
			{
				var reply = await HandleAsync(message.Chat.Id, InboundCommandParser.Parse(text), ct);
				await _channel.SendReplyAsync(message.Chat.Id, reply, ct);
				break;
			}
			case { CallbackQuery: { Data: { } data } query }:
			{
				var chatId = query.Message?.Chat.Id ?? query.From.Id;
				await bot.AnswerCallbackQuery(query.Id, cancellationToken: ct);
				var command = InboundCommandParser.Parse(null, data);
				// The answer to the confirmation buttons replaces them, so they can't be pressed twice
				if (command.Type is InboundCommandType.ReleaseConfirm or InboundCommandType.ReleaseCancel
				    && query.Message is { } confirmation)
					await bot.EditMessageReplyMarkup(chatId, confirmation.MessageId, cancellationToken: ct);
				var reply = await HandleAsync(chatId, command, ct);
				await _channel.SendReplyAsync(chatId, reply, ct);
				break;
			}
		}
	}

	private async Task<InboundReply> HandleAsync(long chatId, InboundCommand command, CancellationToken ct)
	{
		await using var scope = _scopes.CreateAsyncScope();
		var handler = scope.ServiceProvider.GetRequiredService<InboundCommandHandler>();
		return await handler.HandleAsync(NotificationChannel.Telegram, chatId.ToString(), command, ct);
	}

	public async Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, HandleErrorSource source,
		CancellationToken ct)
	{
		_logger.LogWarning(exception, "Telegram {Source}", source);
		// Don't hammer the API when polling fails (network, other instance polling with the same token)
		if (source == HandleErrorSource.PollingError)
			await Task.Delay(TimeSpan.FromSeconds(10), ct);
	}
}
