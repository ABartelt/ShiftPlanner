using System.ComponentModel.DataAnnotations;
using Muddi.ShiftPlanner.Shared.Contracts.v1;

namespace Muddi.ShiftPlanner.Server.Database.Entities;

/// <summary>
/// One reminder for one shift on one channel. The unique key prevents double sends; the row also carries the
/// secrets that let the user release the shift from the reminder.
/// </summary>
public class ShiftNotificationEntity
{
	public Guid Id { get; set; }

	/// <summary>
	/// Null once the shift was deleted, the row is kept so a used release link still says "already released".
	/// </summary>
	public Guid? ShiftId { get; set; }

	public ShiftEntity? Shift { get; set; }
	public Guid EmployeeKeycloakId { get; set; }
	public ReminderKind Kind { get; set; }

	/// <summary>
	/// Shift start at the time of sending; a moved shift is reminded again.
	/// </summary>
	public DateTime ShiftStart { get; set; }

	public NotificationChannel Channel { get; set; }

	[MaxLength(320)]
	public required string Address { get; set; }

	public NotificationStatus Status { get; set; }
	public int Attempts { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime? SentAt { get; set; }

	[MaxLength(500)]
	public string? LastError { get; set; }

	/// <summary>
	/// 6 digit code to release the shift by text message (Signal: "ABGEBEN 123456").
	/// </summary>
	[MaxLength(6)]
	public string? ActionCode { get; set; }

	/// <summary>
	/// Secret of the e-mail release link.
	/// </summary>
	[MaxLength(64)]
	public string? ActionToken { get; set; }

	/// <summary>
	/// Set when the user asked to release the shift and still has to confirm it.
	/// </summary>
	public DateTime? ReleaseRequestedAt { get; set; }

	public DateTime? ActionUsedAt { get; set; }
}

public enum NotificationStatus
{
	Pending,
	Sent,
	Failed
}
