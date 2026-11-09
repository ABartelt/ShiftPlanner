using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using Muddi.ShiftPlanner.Services.Alerting.Notifications;
using Muddi.ShiftPlanner.Services.Alerting.Options;
using Muddi.ShiftPlanner.Shared.Contracts.v1;

namespace Muddi.ShiftPlanner.Services.Alerting.Channels;

public class EmailChannel : INotificationChannel
{
	private readonly SmtpOptions _options;

	public EmailChannel(IOptions<SmtpOptions> options)
	{
		_options = options.Value;
	}

	public NotificationChannel Channel => NotificationChannel.Email;

	public async Task SendReminderAsync(string address, ReminderMessage message, CancellationToken ct)
	{
		var mail = new MimeMessage();
		mail.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
		mail.To.Add(MailboxAddress.Parse(address));
		mail.Subject = ReminderTexts.EmailSubject(message);
		if (message.UnsubscribeUrl is not null)
		{
			// RFC 8058 one-click unsubscribe
			mail.Headers.Add("List-Unsubscribe", $"<{message.UnsubscribeUrl}>");
			mail.Headers.Add("List-Unsubscribe-Post", "List-Unsubscribe=One-Click");
		}

		mail.Body = new BodyBuilder
		{
			TextBody = ReminderTexts.EmailText(message),
			HtmlBody = ReminderTexts.EmailHtml(message)
		}.ToMessageBody();

		using var smtp = new SmtpClient();
		await smtp.ConnectAsync(_options.Host, _options.Port,
			Enum.Parse<SecureSocketOptions>(_options.Security, ignoreCase: true), ct);
		if (!string.IsNullOrEmpty(_options.User))
			await smtp.AuthenticateAsync(_options.User, _options.Password, ct);
		await smtp.SendAsync(mail, ct);
		await smtp.DisconnectAsync(true, ct);
	}
}
