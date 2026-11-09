using System.ComponentModel.DataAnnotations;

namespace Muddi.ShiftPlanner.Server.Database.Entities;

/// <summary>
/// Which reminders a user wants. A missing row means both reminders are on.
/// </summary>
public class NotificationPreferencesEntity
{
	[Key]
	public Guid EmployeeKeycloakId { get; set; }

	public bool RemindDayBefore { get; set; } = true;
	public bool RemindShortBefore { get; set; } = true;
}
