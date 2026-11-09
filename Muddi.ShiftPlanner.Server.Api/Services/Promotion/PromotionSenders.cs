using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Muddi.ShiftPlanner.Shared.Entities;

namespace Muddi.ShiftPlanner.Server.Api.Services.Promotion;

/// <summary>
/// Sending failed; <see cref="Exception.Message"/> is shown to admins.
/// </summary>
public class PromotionSendException(string message) : Exception(message);

public record TelegramChat(string Id, string Title, string Type);

public record SignalGroup(string Id, string Name);

/// <summary>
/// HTTP clients for the messengers. Each method throws <see cref="PromotionSendException"/> on failure.
/// </summary>
public class PromotionSenders(IHttpClientFactory httpClientFactory, IConfiguration configuration)
{
	public const string HttpClientName = "promotion";
	private const string SlackWebhookPrefix = "https://hooks.slack.com/";

	private HttpClient Http => httpClientFactory.CreateClient(HttpClientName);

	/// <summary>
	/// Default URL of the signal-cli-rest-api container (docker-compose service "signal-api").
	/// </summary>
	public string DefaultSignalServiceUrl => configuration["Promotion:SignalServiceUrl"] ?? "http://signal-api:8080";

	public static PromotionFormat FormatOf(PromotionChannelKind kind) => kind switch
	{
		PromotionChannelKind.Telegram => PromotionFormat.Telegram,
		PromotionChannelKind.Slack => PromotionFormat.Slack,
		PromotionChannelKind.Signal => PromotionFormat.Signal,
		_ => PromotionFormat.Plain
	};

	/// <summary>
	/// Returns null when the channel is complete, otherwise what is missing.
	/// </summary>
	public static string? Validate(PromotionChannelKind kind, PromotionChannelConfig config, string? secret) => kind switch
	{
		PromotionChannelKind.Telegram when string.IsNullOrWhiteSpace(secret) => "Bot-Token fehlt",
		PromotionChannelKind.Telegram when string.IsNullOrWhiteSpace(config.Target) => "Chat fehlt",
		PromotionChannelKind.Slack when string.IsNullOrWhiteSpace(secret) => "Webhook-URL fehlt",
		PromotionChannelKind.Slack when !secret!.StartsWith(SlackWebhookPrefix, StringComparison.Ordinal) =>
			$"Die Webhook-URL muss mit {SlackWebhookPrefix} beginnen",
		PromotionChannelKind.Signal when string.IsNullOrWhiteSpace(config.SignalNumber) => "Signal-Nummer fehlt",
		PromotionChannelKind.Signal when string.IsNullOrWhiteSpace(config.Target) => "Signal-Gruppe fehlt",
		_ when !string.IsNullOrWhiteSpace(config.ServiceUrl) && !IsHttpUrl(config.ServiceUrl) =>
			"Die Service-URL muss mit http:// oder https:// beginnen",
		_ => null
	};

	public Task SendAsync(PromotionChannelKind kind, PromotionChannelConfig config, string? secret, string text,
		string signUpUrl, CancellationToken ct)
	{
		if (Validate(kind, config, secret) is { } missing)
			throw new PromotionSendException(missing);
		return kind switch
		{
			PromotionChannelKind.Telegram => SendTelegramAsync(secret!, config.Target, text, ct),
			PromotionChannelKind.Slack => SendSlackAsync(secret!, text, signUpUrl, ct),
			PromotionChannelKind.Signal => SendSignalAsync(SignalUrl(config.ServiceUrl), config.SignalNumber, config.Target, text, ct),
			_ => throw new PromotionSendException($"Unbekannter Kanal {kind}")
		};
	}

	// ---- Telegram (Bot API) ----

	private async Task SendTelegramAsync(string token, string chatId, string html, CancellationToken ct)
	{
		await TelegramAsync(token, "sendMessage", new
		{
			chat_id = chatId,
			text = html,
			parse_mode = "HTML",
			link_preview_options = new { is_disabled = true }
		}, ct);
	}

	/// <summary>
	/// Chats the bot has recently seen. The bot only sees a group after it was added and someone wrote a message.
	/// </summary>
	public async Task<List<TelegramChat>> GetTelegramChatsAsync(string token, CancellationToken ct)
	{
		var result = await TelegramAsync(token, "getUpdates", new { allowed_updates = new[] { "message", "channel_post", "my_chat_member" } }, ct);
		return result.AsArray()
			.Select(update => (update?["message"] ?? update?["channel_post"] ?? update?["my_chat_member"])?["chat"])
			.OfType<JsonNode>()
			.Select(chat => new TelegramChat(
				chat["id"]!.ToString(),
				chat["title"]?.GetValue<string>() ?? chat["username"]?.GetValue<string>() ?? chat["first_name"]?.GetValue<string>() ?? chat["id"]!.ToString(),
				chat["type"]?.GetValue<string>() ?? ""))
			.DistinctBy(c => c.Id)
			.ToList();
	}

	private async Task<JsonNode> TelegramAsync(string token, string method, object body, CancellationToken ct)
	{
		using var response = await PostAsync($"https://api.telegram.org/bot{token}/{method}", JsonContent.Create(body), "Telegram", ct);
		var json = await ReadJsonAsync(response, ct);
		if (json?["ok"]?.GetValue<bool>() != true)
			throw new PromotionSendException($"Telegram: {json?["description"]?.GetValue<string>() ?? response.StatusCode.ToString()}");
		return json["result"]!;
	}

	// ---- Slack (Incoming Webhook) ----

	private async Task SendSlackAsync(string webhookUrl, string mrkdwn, string signUpUrl, CancellationToken ct)
	{
		// Section blocks are limited to 3000 characters; the formatter keeps Slack texts below that.
		var payload = new
		{
			text = mrkdwn,
			unfurl_links = false,
			blocks = new object[]
			{
				new { type = "section", text = new { type = "mrkdwn", text = mrkdwn } },
				new
				{
					type = "actions",
					elements = new object[]
					{
						new { type = "button", text = new { type = "plain_text", text = "Jetzt eintragen" }, url = signUpUrl, style = "primary" }
					}
				}
			}
		};
		using var response = await PostAsync(webhookUrl, JsonContent.Create(payload), "Slack", ct);
		if (!response.IsSuccessStatusCode)
			throw new PromotionSendException($"Slack: {await response.Content.ReadAsStringAsync(ct)} ({(int)response.StatusCode})");
	}

	// ---- Signal (signal-cli-rest-api) ----

	public string SignalUrl(string? serviceUrl) =>
		(string.IsNullOrWhiteSpace(serviceUrl) ? DefaultSignalServiceUrl : serviceUrl).TrimEnd('/');

	private async Task SendSignalAsync(string serviceUrl, string number, string groupId, string text, CancellationToken ct)
	{
		var body = new { message = text, number, recipients = new[] { groupId }, text_mode = "styled" };
		using var response = await PostAsync($"{serviceUrl}/v2/send", JsonContent.Create(body), "Signal", ct);
		if (!response.IsSuccessStatusCode)
			throw new PromotionSendException($"Signal: {await SignalErrorAsync(response, ct)}");
	}

	/// <summary>
	/// Numbers registered or linked in the signal-cli-rest-api container.
	/// </summary>
	public async Task<List<string>> GetSignalAccountsAsync(string? serviceUrl, CancellationToken ct)
	{
		using var response = await GetAsync($"{SignalUrl(serviceUrl)}/v1/accounts", "Signal", ct);
		if (!response.IsSuccessStatusCode)
			throw new PromotionSendException($"Signal: {await SignalErrorAsync(response, ct)}");
		return await response.Content.ReadFromJsonAsync<List<string>>(ct) ?? [];
	}

	public async Task<List<SignalGroup>> GetSignalGroupsAsync(string? serviceUrl, string number, CancellationToken ct)
	{
		using var response = await GetAsync($"{SignalUrl(serviceUrl)}/v1/groups/{Uri.EscapeDataString(number)}", "Signal", ct);
		if (!response.IsSuccessStatusCode)
			throw new PromotionSendException($"Signal: {await SignalErrorAsync(response, ct)}");
		var json = await ReadJsonAsync(response, ct);
		return json?.AsArray()
			.OfType<JsonNode>()
			.Select(g => new SignalGroup(g["id"]!.GetValue<string>(), g["name"]?.GetValue<string>() ?? g["id"]!.GetValue<string>()))
			.ToList() ?? [];
	}

	/// <summary>
	/// QR code (PNG) to link the container as a secondary device of an existing Signal account.
	/// </summary>
	public async Task<byte[]> GetSignalLinkQrCodeAsync(string? serviceUrl, CancellationToken ct)
	{
		using var response = await GetAsync($"{SignalUrl(serviceUrl)}/v1/qrcodelink?device_name=Schichtplaner", "Signal", ct);
		if (!response.IsSuccessStatusCode)
			throw new PromotionSendException($"Signal: {await SignalErrorAsync(response, ct)}");
		return await response.Content.ReadAsByteArrayAsync(ct);
	}

	private static async Task<string> SignalErrorAsync(HttpResponseMessage response, CancellationToken ct)
	{
		var text = await response.Content.ReadAsStringAsync(ct);
		try
		{
			return JsonNode.Parse(text)?["error"]?.GetValue<string>() ?? text;
		}
		catch (JsonException)
		{
			return string.IsNullOrWhiteSpace(text) ? response.StatusCode.ToString() : text;
		}
	}

	// ---- helpers ----

	private static bool IsHttpUrl(string url) =>
		Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

	private Task<HttpResponseMessage> PostAsync(string url, HttpContent content, string service, CancellationToken ct) =>
		CallAsync(() => Http.PostAsync(url, content, ct), service);

	private Task<HttpResponseMessage> GetAsync(string url, string service, CancellationToken ct) =>
		CallAsync(() => Http.GetAsync(url, ct), service);

	private static async Task<HttpResponseMessage> CallAsync(Func<Task<HttpResponseMessage>> call, string service)
	{
		try
		{
			return await call();
		}
		catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
		{
			// Don't include the URL: it may contain the bot token.
			throw new PromotionSendException($"{service} ist nicht erreichbar ({e.GetType().Name})");
		}
	}

	private static async Task<JsonNode?> ReadJsonAsync(HttpResponseMessage response, CancellationToken ct)
	{
		try
		{
			return JsonNode.Parse(await response.Content.ReadAsStringAsync(ct));
		}
		catch (JsonException)
		{
			throw new PromotionSendException($"Unerwartete Antwort ({(int)response.StatusCode})");
		}
	}
}
