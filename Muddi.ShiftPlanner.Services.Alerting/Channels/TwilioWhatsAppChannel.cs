using System.Text.Json;
using Microsoft.Extensions.Options;
using Muddi.ShiftPlanner.Services.Alerting.Notifications;
using Muddi.ShiftPlanner.Services.Alerting.Options;
using Muddi.ShiftPlanner.Shared.Contracts.v1;
using Twilio.Clients;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace Muddi.ShiftPlanner.Services.Alerting.Channels;

public class TwilioWhatsAppChannel : INotificationChannel
{
	private readonly ITwilioRestClient _client;
	private readonly TwilioOptions _options;

	public TwilioWhatsAppChannel(ITwilioRestClient client, IOptions<TwilioOptions> options)
	{
		_client = client;
		_options = options.Value;
	}

	public NotificationChannel Channel => NotificationChannel.WhatsApp;

	public async Task SendReminderAsync(string address, ReminderMessage message, CancellationToken ct)
	{
		var templates = _options.Templates;
		var contentSid = message.Kind switch
		{
			ReminderKind.DayBefore when !message.CanRelease && !string.IsNullOrEmpty(templates.DayBeforeNoRelease)
				=> templates.DayBeforeNoRelease,
			ReminderKind.DayBefore => templates.DayBefore,
			_ => templates.ShortBefore
		};

		var options = new CreateMessageOptions(To(address)) { From = To(_options.WhatsAppFrom) };
		if (string.IsNullOrEmpty(contentSid))
		{
			// Only delivered inside the 24h session window (or in the Twilio sandbox)
			options.Body = ReminderTexts.PlainText(message, releaseByCode: false);
		}
		else
		{
			options.ContentSid = contentSid;
			options.ContentVariables = JsonSerializer.Serialize(ReminderTexts.WhatsAppVariables(message));
		}

		await MessageResource.CreateAsync(options, _client);
	}

	private static PhoneNumber To(string number) => new("whatsapp:" + number);
}
