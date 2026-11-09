using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Muddi.ShiftPlanner.Server.Api.Extensions;
using Muddi.ShiftPlanner.Server.Database.Contexts;
using Muddi.ShiftPlanner.Server.Database.Entities;
using Muddi.ShiftPlanner.Shared;

namespace Muddi.ShiftPlanner.Server.Api.Endpoints.Notifications;

/// <summary>
/// Starts linking a messenger: returns a code the user sends to the bot, which then binds the sender to the user.
/// E-mail is linked right away with the verified address of the Keycloak account.
/// </summary>
public class CreateChannelLinkEndpoint : CrudEndpoint<NotificationChannelRequest, CreateChannelLinkResponse>
{
	private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(15);
	private readonly NotificationChannelSettings _channels;

	public CreateChannelLinkEndpoint(ShiftPlannerContext database, NotificationChannelSettings channels) :
		base(database)
	{
		_channels = channels;
	}

	protected override void CrudConfigure()
	{
		Roles(ApiRoles.Editor, ApiRoles.Viewer);
		Post("/me/notifications/channels/{Channel}/link");
	}

	public override async Task HandleAsync(NotificationChannelRequest req, CancellationToken ct)
	{
		if (!_channels.IsAvailable(req.Channel))
		{
			await Send.ConflictAsync($"{req.Channel} notifications are not available", ct);
			return;
		}

		var userId = User.GetKeycloakId();
		if (req.Channel == NotificationChannel.Email)
		{
			await LinkEmailAsync(userId, ct);
			return;
		}

		var now = DateTime.UtcNow;
		await Database.NotificationLinkCodes
			.Where(c => c.ExpiresAt < now || (c.EmployeeKeycloakId == userId && c.Channel == req.Channel))
			.ExecuteDeleteAsync(ct);

		var code = new NotificationLinkCodeEntity
		{
			Code = NotificationExtensions.CreateLinkCode(),
			EmployeeKeycloakId = userId,
			Channel = req.Channel,
			ExpiresAt = now + CodeLifetime
		};
		Database.NotificationLinkCodes.Add(code);
		await Database.SaveChangesAsync(ct);

		Response = new CreateChannelLinkResponse
		{
			Channel = req.Channel,
			Linked = false,
			Code = code.Code,
			ExpiresAt = code.ExpiresAt,
			DeepLink = DeepLink(req.Channel, code.Code),
			Instructions = Instructions(req.Channel, code.Code)
		};
	}

	private async Task LinkEmailAsync(Guid userId, CancellationToken ct)
	{
		var email = User.FindFirst("email")?.Value;
		var verified = string.Equals(User.FindFirst("email_verified")?.Value, "true",
			StringComparison.OrdinalIgnoreCase);
		if (string.IsNullOrEmpty(email) || !verified)
		{
			await Send.ConflictAsync("Your account has no verified e-mail address", ct);
			return;
		}

		var link = await Database.NotificationChannelLinks
			.FirstOrDefaultAsync(l => l.EmployeeKeycloakId == userId && l.Channel == NotificationChannel.Email, ct);
		if (link is null)
		{
			link = new NotificationChannelLinkEntity
			{
				Id = Guid.NewGuid(),
				EmployeeKeycloakId = userId,
				Channel = NotificationChannel.Email,
				Address = email,
				UnsubscribeToken = NotificationExtensions.CreateToken()
			};
			Database.NotificationChannelLinks.Add(link);
		}

		// Linking again picks up an address changed in Keycloak and re-enables after an unsubscribe
		link.Address = email;
		link.Enabled = true;
		link.LinkedAt = DateTime.UtcNow;
		await Database.SaveChangesAsync(ct);

		Response = new CreateChannelLinkResponse
		{
			Channel = NotificationChannel.Email,
			Linked = true,
			Instructions = $"Erinnerungen werden an {NotificationExtensions.MaskAddress(email)} gesendet."
		};
	}

	private string? DeepLink(NotificationChannel channel, string code) => channel switch
	{
		NotificationChannel.Telegram => $"https://t.me/{_channels.TelegramBotUsername}?start={code}",
		NotificationChannel.WhatsApp =>
			$"https://wa.me/{Digits(_channels.WhatsAppNumber!)}?text={Uri.EscapeDataString("LINK " + code)}",
		NotificationChannel.Signal => $"https://signal.me/#p/+{Digits(_channels.SignalNumber!)}",
		_ => null
	};

	private string Instructions(NotificationChannel channel, string code) => channel switch
	{
		NotificationChannel.Telegram =>
			$"Öffne den Link und tippe in Telegram auf „Starten“. Alternativ sende „/start {code}“ an @{_channels.TelegramBotUsername}.",
		NotificationChannel.WhatsApp =>
			$"Öffne den Link und sende die vorbereitete Nachricht „LINK {code}“ an {_channels.WhatsAppNumber}.",
		NotificationChannel.Signal => $"Sende „LINK {code}“ per Signal an {_channels.SignalNumber}.",
		_ => string.Empty
	};

	private static string Digits(string number) => new(number.Where(char.IsDigit).ToArray());
}
