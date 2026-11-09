namespace Muddi.ShiftPlanner.Shared.Contracts.v1.Responses;

public class GetNotificationSettingsResponse
{
	public bool RemindDayBefore { get; set; } = true;
	public bool RemindShortBefore { get; set; } = true;

	/// <summary>
	/// Channels the user has linked.
	/// </summary>
	public List<NotificationChannelResponse> Channels { get; set; } = new();

	/// <summary>
	/// Channels this installation offers.
	/// </summary>
	public List<NotificationChannel> AvailableChannels { get; set; } = new();
}

public class NotificationChannelResponse
{
	public NotificationChannel Channel { get; set; }

	/// <summary>
	/// Masked address, e.g. <c>+49•••123</c> or <c>m•••@example.org</c>.
	/// </summary>
	public string Address { get; set; } = string.Empty;

	/// <summary>
	/// False after the user replied STOP or unsubscribed; linking again enables it.
	/// </summary>
	public bool Enabled { get; set; }

	public DateTime LinkedAt { get; set; }
}
