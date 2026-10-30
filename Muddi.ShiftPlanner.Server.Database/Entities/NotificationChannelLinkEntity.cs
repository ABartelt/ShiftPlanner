using System.ComponentModel.DataAnnotations;
using Muddi.ShiftPlanner.Shared.Contracts.v1;

namespace Muddi.ShiftPlanner.Server.Database.Entities;

/// <summary>
/// A messenger account or e-mail address of a user that reminders are sent to. One per user and channel.
/// </summary>
public class NotificationChannelLinkEntity
{
	public Guid Id { get; set; }
	public Guid EmployeeKeycloakId { get; set; }
	public NotificationChannel Channel { get; set; }

	/// <summary>
	/// Telegram chat id, E.164 phone number (WhatsApp, Signal) or e-mail address.
	/// </summary>
	[MaxLength(320)]
	public required string Address { get; set; }

	/// <summary>
	/// False after the user replied STOP or unsubscribed.
	/// </summary>
	public bool Enabled { get; set; } = true;

	public DateTime LinkedAt { get; set; }

	/// <summary>
	/// Secret for the e-mail unsubscribe link.
	/// </summary>
	[MaxLength(64)]
	public required string UnsubscribeToken { get; set; }
}
