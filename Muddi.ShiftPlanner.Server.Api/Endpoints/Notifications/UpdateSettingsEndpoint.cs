using FastEndpoints;
using Muddi.ShiftPlanner.Server.Api.Extensions;
using Muddi.ShiftPlanner.Server.Database.Contexts;
using Muddi.ShiftPlanner.Shared;

namespace Muddi.ShiftPlanner.Server.Api.Endpoints.Notifications;

/// <summary>
/// Turns the day-before and 30-minute reminders of the calling user on or off.
/// </summary>
public class UpdateSettingsEndpoint : CrudUpdateEndpoint<UpdateNotificationSettingsRequest>
{
	public UpdateSettingsEndpoint(ShiftPlannerContext database) : base(database)
	{
	}

	protected override void CrudConfigure()
	{
		Roles(ApiRoles.Editor, ApiRoles.Viewer);
		Put("/me/notifications");
	}

	public override async Task CrudExecuteAsync(UpdateNotificationSettingsRequest request, CancellationToken ct)
	{
		var prefs = await Database.GetOrCreatePreferencesAsync(User.GetKeycloakId(), ct);
		prefs.RemindDayBefore = request.RemindDayBefore;
		prefs.RemindShortBefore = request.RemindShortBefore;
		await Database.SaveChangesAsync(ct);
	}
}
