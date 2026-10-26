using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Muddi.ShiftPlanner.Server.Database.Contexts;
using Muddi.ShiftPlanner.Server.Database.Entities;

namespace Muddi.ShiftPlanner.Server.Api.Extensions;

/// <summary>
/// Reads the <c>Notifications</c> section. The API only needs the bot handles to build deep links,
/// sending happens in Services.Alerting.
/// </summary>
public class NotificationChannelSettings
{
	public NotificationChannelSettings(IConfiguration configuration)
	{
		var section = configuration.GetSection("Notifications");
		TelegramBotUsername = section["Telegram:BotUsername"]?.TrimStart('@');
		WhatsAppNumber = section["WhatsApp:Number"];
		SignalNumber = section["Signal:Number"];
		EmailEnabled = section.GetValue("Email:Enabled", false);
	}

	public string? TelegramBotUsername { get; }
	public string? WhatsAppNumber { get; }
	public string? SignalNumber { get; }
	public bool EmailEnabled { get; }

	public bool IsAvailable(NotificationChannel channel) => channel switch
	{
		NotificationChannel.Telegram => !string.IsNullOrEmpty(TelegramBotUsername),
		NotificationChannel.WhatsApp => !string.IsNullOrEmpty(WhatsAppNumber),
		NotificationChannel.Signal => !string.IsNullOrEmpty(SignalNumber),
		NotificationChannel.Email => EmailEnabled,
		_ => false
	};

	public List<NotificationChannel> AvailableChannels
		=> Enum.GetValues<NotificationChannel>().Where(IsAvailable).ToList();
}

public static class NotificationExtensions
{
	// No 0/O and 1/I so codes can be typed from a screen
	private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

	public static string CreateLinkCode() => RandomNumberGenerator.GetString(CodeAlphabet, 8);

	public static string CreateToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
		.TrimEnd('=').Replace('+', '-').Replace('/', '_');

	/// <summary>
	/// <c>+49•••123</c>, <c>m•••@example.org</c>, <c>•••4711</c>
	/// </summary>
	public static string MaskAddress(string address)
	{
		var at = address.IndexOf('@');
		if (at > 0)
			return address[0] + "•••" + address[at..];
		if (address.Length <= 6)
			return "•••" + address[^Math.Min(2, address.Length)..];
		var head = address.StartsWith('+') ? 3 : 0;
		return address[..head] + "•••" + address[^3..];
	}

	public static async Task<NotificationPreferencesEntity> GetOrCreatePreferencesAsync(this ShiftPlannerContext db,
		Guid employeeId, CancellationToken ct)
	{
		var prefs = await db.NotificationPreferences.FirstOrDefaultAsync(p => p.EmployeeKeycloakId == employeeId, ct);
		if (prefs is not null)
			return prefs;
		prefs = new NotificationPreferencesEntity { EmployeeKeycloakId = employeeId };
		db.NotificationPreferences.Add(prefs);
		return prefs;
	}
}
