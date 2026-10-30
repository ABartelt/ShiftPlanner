namespace Muddi.ShiftPlanner.Shared.Contracts.v1.Requests;

public class UpdateNotificationSettingsRequest
{
	public bool RemindDayBefore { get; set; } = true;
	public bool RemindShortBefore { get; set; } = true;
}
