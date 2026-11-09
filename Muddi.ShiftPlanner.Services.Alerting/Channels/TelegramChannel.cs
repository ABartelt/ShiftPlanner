using Muddi.ShiftPlanner.Services.Alerting.Inbound;
using Muddi.ShiftPlanner.Services.Alerting.Notifications;
using Muddi.ShiftPlanner.Shared.Contracts.v1;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Muddi.ShiftPlanner.Services.Alerting.Channels;

public class TelegramChannel : INotificationChannel
{
	private readonly ITelegramBotClient _bot;

	public TelegramChannel(ITelegramBotClient bot)
	{
		_bot = bot;
	}

	public NotificationChannel Channel => NotificationChannel.Telegram;

	public async Task SendReminderAsync(string address, ReminderMessage message, CancellationToken ct)
	{
		var rows = new List<InlineKeyboardButton[]>();
		if (message.CanRelease)
			rows.Add([InlineKeyboardButton.WithCallbackData("Schicht abgeben", InboundPayloads.Release(message.NotificationId))]);
		// Telegram rejects URL buttons pointing to localhost
		if (IsPublicUrl(message.ShiftLink))
			rows.Add([
				InlineKeyboardButton.WithUrl(
					message.Kind == ReminderKind.DayBefore ? "Andere Schicht suchen" : "Im Schichtplan öffnen",
					message.ShiftLink)
			]);

		await _bot.SendMessage(long.Parse(address), ReminderTexts.TelegramHtml(message), ParseMode.Html,
			replyMarkup: rows.Count > 0 ? new InlineKeyboardMarkup(rows) : null,
			cancellationToken: ct);
	}

	public Task SendReplyAsync(long chatId, InboundReply reply, CancellationToken ct)
	{
		var keyboard = reply.ConfirmReleaseOf is { } id
			? new InlineKeyboardMarkup([
				[
					InlineKeyboardButton.WithCallbackData("Ja, abgeben", InboundPayloads.Confirm(id)),
					InlineKeyboardButton.WithCallbackData("Nein", InboundPayloads.Cancel(id))
				]
			])
			: null;
		return _bot.SendMessage(chatId, reply.Text, replyMarkup: keyboard, cancellationToken: ct);
	}

	private static bool IsPublicUrl(string url)
		=> Uri.TryCreate(url, UriKind.Absolute, out var uri)
		   && uri.Scheme is "https" or "http"
		   && !uri.IsLoopback;
}
