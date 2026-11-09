using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Muddi.ShiftPlanner.Services.Alerting.Notifications;
using Muddi.ShiftPlanner.Services.Alerting.Options;
using Muddi.ShiftPlanner.Shared.Contracts.v1;

namespace Muddi.ShiftPlanner.Services.Alerting.Channels;

/// <summary>
/// Talks to a self-hosted <a href="https://github.com/bbernhard/signal-cli-rest-api">signal-cli-rest-api</a>.
/// Signal has no buttons, so the shift is released by replying with the action code.
/// </summary>
public class SignalChannel : INotificationChannel
{
	public const string HttpClientName = "signal";
	private readonly IHttpClientFactory _httpClientFactory;
	private readonly SignalOptions _options;

	public SignalChannel(IHttpClientFactory httpClientFactory, IOptions<SignalOptions> options)
	{
		_httpClientFactory = httpClientFactory;
		_options = options.Value;
	}

	public NotificationChannel Channel => NotificationChannel.Signal;

	public Task SendReminderAsync(string address, ReminderMessage message, CancellationToken ct)
		=> SendTextAsync(address, ReminderTexts.PlainText(message, releaseByCode: true), ct);

	public async Task SendTextAsync(string address, string text, CancellationToken ct)
	{
		var response = await Client.PostAsJsonAsync("v2/send",
			new SendRequest(text, _options.Number, [address]), ct);
		response.EnsureSuccessStatusCode();
	}

	/// <summary>
	/// Fetches and acknowledges the messages received since the last call.
	/// </summary>
	public async Task<IReadOnlyList<(string Sender, string Text)>> ReceiveAsync(CancellationToken ct)
	{
		var envelopes = await Client.GetFromJsonAsync<List<ReceivedMessage>>(
			$"v1/receive/{Uri.EscapeDataString(_options.Number)}?timeout=1", ct) ?? [];
		return envelopes
			.Select(e => e.Envelope)
			.Where(e => e?.DataMessage?.Message is not null && (e.SourceNumber ?? e.Source) is not null)
			.Select(e => (e!.SourceNumber ?? e.Source!, e.DataMessage!.Message!))
			.ToList();
	}

	private HttpClient Client => _httpClientFactory.CreateClient(HttpClientName);

	private record SendRequest(
		[property: JsonPropertyName("message")] string Message,
		[property: JsonPropertyName("number")] string Number,
		[property: JsonPropertyName("recipients")] string[] Recipients);

	private record ReceivedMessage([property: JsonPropertyName("envelope")] Envelope? Envelope);

	private record Envelope(
		[property: JsonPropertyName("source")] string? Source,
		[property: JsonPropertyName("sourceNumber")] string? SourceNumber,
		[property: JsonPropertyName("dataMessage")] DataMessage? DataMessage);

	private record DataMessage([property: JsonPropertyName("message")] string? Message);
}
