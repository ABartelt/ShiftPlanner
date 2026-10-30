using System.Text.Json.Serialization;

namespace Muddi.ShiftPlanner.Shared.Contracts.v1;

[JsonConverter(typeof(JsonStringEnumConverter<NotificationChannel>))]
public enum NotificationChannel
{
	Telegram,
	WhatsApp,
	Signal,
	Email
}

[JsonConverter(typeof(JsonStringEnumConverter<ReminderKind>))]
public enum ReminderKind
{
	/// <summary>
	/// 24 hours before the shift, offers to release it.
	/// </summary>
	DayBefore,

	/// <summary>
	/// 30 minutes before the shift.
	/// </summary>
	ShortBefore
}
