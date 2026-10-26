using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Muddi.ShiftPlanner.Server.Database.Contexts;
using Muddi.ShiftPlanner.Shared;

namespace Muddi.ShiftPlanner.Server.Api.Endpoints.Notifications;

/// <summary>
/// Unlinks a messenger of the calling user and deletes the stored address.
/// </summary>
public class DeleteChannelEndpoint : CrudEndpoint<NotificationChannelRequest, EmptyResponse>
{
	public DeleteChannelEndpoint(ShiftPlannerContext database) : base(database)
	{
	}

	protected override void CrudConfigure()
	{
		Roles(ApiRoles.Editor, ApiRoles.Viewer);
		Delete("/me/notifications/channels/{Channel}");
	}

	public override async Task HandleAsync(NotificationChannelRequest req, CancellationToken ct)
	{
		var userId = User.GetKeycloakId();
		var deleted = await Database.NotificationChannelLinks
			.Where(l => l.EmployeeKeycloakId == userId && l.Channel == req.Channel)
			.ExecuteDeleteAsync(ct);
		if (deleted == 0)
		{
			await Send.NotFoundAsync("Channel", ct);
			return;
		}

		// Sent reminders are kept for a while for deduplication, but without the address
		await Database.ShiftNotifications
			.Where(n => n.EmployeeKeycloakId == userId && n.Channel == req.Channel)
			.ExecuteUpdateAsync(u => u.SetProperty(n => n.Address, string.Empty), ct);

		await Send.NoContentWith200Async(ct);
	}
}
