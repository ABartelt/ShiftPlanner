using System.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Muddi.ShiftPlanner.Server.Database.Contexts;
using Muddi.ShiftPlanner.Services.Alerting.Inbound;
using Muddi.ShiftPlanner.Services.Alerting.Notifications;
using Muddi.ShiftPlanner.Services.Alerting.Options;
using Muddi.ShiftPlanner.Shared.Contracts.v1;
using Twilio.Security;

namespace Muddi.ShiftPlanner.Services.Alerting.Endpoints;

public static class NotificationEndpoints
{
	/// <summary>
	/// Release and unsubscribe links of the reminder e-mails. The secret token in the URL is the authorization.
	/// </summary>
	public static void MapEmailActionEndpoints(this WebApplication app)
	{
		var actions = app.MapGroup("/actions").AddEndpointFilter(async (context, next) =>
		{
			// The token is in the URL: don't leak it to linked sites, caches or search engines
			var headers = context.HttpContext.Response.Headers;
			headers.CacheControl = "no-store";
			headers["Referrer-Policy"] = "no-referrer";
			headers["X-Robots-Tag"] = "noindex";
			return await next(context);
		});

		actions.MapGet("/release/{token}", async (string token, ShiftReleaseService release, CancellationToken ct) =>
		{
			var notification = await release.FindByTokenAsync(token, ct);
			if (notification is null)
				return Html(ActionPages.NotFound, StatusCodes.Status404NotFound);
			var check = await release.CheckAsync(notification, ct);
			return Html(check.Outcome == ReleaseOutcome.Released
				? ActionPages.ConfirmRelease(check.Summary!)
				: ActionPages.ReleaseResult(check));
		});

		actions.MapPost("/release/{token}", async (string token, ShiftReleaseService release, CancellationToken ct) =>
		{
			var notification = await release.FindByTokenAsync(token, ct);
			if (notification is null)
				return Html(ActionPages.NotFound, StatusCodes.Status404NotFound);
			return Html(ActionPages.ReleaseResult(await release.ReleaseAsync(notification, ct)));
		});

		actions.MapGet("/unsubscribe/{token}", async (string token, ShiftPlannerContext db, CancellationToken ct) =>
			await db.NotificationChannelLinks.AnyAsync(l => l.UnsubscribeToken == token, ct)
				? Html(ActionPages.ConfirmUnsubscribe)
				: Html(ActionPages.NotFound, StatusCodes.Status404NotFound));

		// Also the RFC 8058 one-click target of the List-Unsubscribe header
		actions.MapPost("/unsubscribe/{token}", async (string token, ShiftPlannerContext db, CancellationToken ct) =>
		{
			var updated = await db.NotificationChannelLinks
				.Where(l => l.UnsubscribeToken == token)
				.ExecuteUpdateAsync(u => u.SetProperty(l => l.Enabled, false), ct);
			return updated > 0
				? Html(ActionPages.Unsubscribed)
				: Html(ActionPages.NotFound, StatusCodes.Status404NotFound);
		});
	}

	/// <summary>
	/// Incoming WhatsApp messages and quick-reply button presses; the answer goes back as TwiML.
	/// </summary>
	public static void MapTwilioWebhook(this WebApplication app)
	{
		app.MapPost("/webhooks/twilio/whatsapp", async (HttpRequest request, IOptions<TwilioOptions> twilio,
			IOptions<NotificationOptions> notifications, InboundCommandHandler handler, ILogger<TwilioWebhook> logger,
			CancellationToken ct) =>
		{
			var form = await request.ReadFormAsync(ct);
			var parameters = form.ToDictionary(f => f.Key, f => f.Value.ToString());
			// Behind a reverse proxy the request URL differs from the one Twilio signed
			var url = notifications.Value.PublicBaseUrl.TrimEnd('/') + request.Path + request.QueryString;
			var signature = request.Headers["X-Twilio-Signature"].ToString();
			if (!new RequestValidator(twilio.Value.AuthToken).Validate(url, parameters, signature))
			{
				logger.LogWarning("Rejected Twilio webhook with invalid signature for {Url}", url);
				return Results.StatusCode(StatusCodes.Status403Forbidden);
			}

			var from = parameters.GetValueOrDefault("From", string.Empty).Replace("whatsapp:", string.Empty);
			var command = InboundCommandParser.Parse(parameters.GetValueOrDefault("Body"),
				parameters.GetValueOrDefault("ButtonPayload"));
			var reply = await handler.HandleAsync(NotificationChannel.WhatsApp, from, command, ct);
			return Results.Content(
				$"<?xml version=\"1.0\" encoding=\"UTF-8\"?><Response><Message>{SecurityElement.Escape(reply.Text)}</Message></Response>",
				"text/xml");
		});
	}

	private static IResult Html(string html, int statusCode = StatusCodes.Status200OK)
		=> Results.Content(html, "text/html; charset=utf-8", statusCode: statusCode);

	/// <summary>
	/// Logger category.
	/// </summary>
	public class TwilioWebhook;
}
