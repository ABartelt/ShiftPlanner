using System.ComponentModel.DataAnnotations;
using Muddi.ShiftPlanner.Shared.Contracts.v1;

namespace Muddi.ShiftPlanner.Server.Database.Entities;

/// <summary>
/// Short-lived code the user sends to a bot to link the messenger account with the shift planner account.
/// </summary>
public class NotificationLinkCodeEntity
{
	[Key]
	[MaxLength(16)]
	public required string Code { get; set; }

	public Guid EmployeeKeycloakId { get; set; }
	public NotificationChannel Channel { get; set; }
	public DateTime ExpiresAt { get; set; }
}
