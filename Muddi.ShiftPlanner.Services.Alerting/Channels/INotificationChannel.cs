using System.Diagnostics.CodeAnalysis;
using Muddi.ShiftPlanner.Services.Alerting.Notifications;
using Muddi.ShiftPlanner.Shared.Contracts.v1;

namespace Muddi.ShiftPlanner.Services.Alerting.Channels;

public interface INotificationChannel
{
	NotificationChannel Channel { get; }

	/// <param name="address">Telegram chat id, E.164 phone number or e-mail address.</param>
	Task SendReminderAsync(string address, ReminderMessage message, CancellationToken ct);
}

/// <summary>
/// Channels whose configuration is complete; the others are not registered.
/// </summary>
public class NotificationChannelRegistry
{
	private readonly Dictionary<NotificationChannel, INotificationChannel> _channels;

	public NotificationChannelRegistry(IEnumerable<INotificationChannel> channels)
	{
		_channels = channels.ToDictionary(c => c.Channel);
	}

	public IReadOnlyCollection<NotificationChannel> Available => _channels.Keys;

	public bool TryGet(NotificationChannel channel, [NotNullWhen(true)] out INotificationChannel? implementation)
		=> _channels.TryGetValue(channel, out implementation);
}
