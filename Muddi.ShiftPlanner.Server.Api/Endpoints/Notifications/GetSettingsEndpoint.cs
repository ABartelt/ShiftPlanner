using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Muddi.ShiftPlanner.Server.Api.Extensions;
using Muddi.ShiftPlanner.Server.Database.Contexts;
using Muddi.ShiftPlanner.Shared;

namespace Muddi.ShiftPlanner.Server.Api.Endpoints.Notifications;

/// <summary>
/// Reminder preferences and linked messengers of the calling user.
/// </summary>
public class GetSettingsEndpoint : CrudEndpointWithoutRequest<GetNotificationSettingsResponse>
{
	private readonly NotificationChannelSettings _channels;

	public GetSettingsEndpoint(ShiftPlannerContext database, NotificationChannelSettings channels) : base(database)
	{
		_channels = channels;
	}

	protected override void CrudConfigure()
	{
		Roles(ApiRoles.Editor, ApiRoles.Viewer);
		Get("/me/notifications");
	}

	public override async Task<GetNotificationSettingsResponse> ExecuteAsync(EmptyRequest req, CancellationToken ct)
	{
		var userId = User.GetKeycloakId();
		var prefs = await Database.NotificationPreferences.AsNoTracking()
			.FirstOrDefaultAsync(p => p.EmployeeKeycloakId == userId, ct);
		var links = await Database.NotificationChannelLinks.AsNoTracking()
			.Where(l => l.EmployeeKeycloakId == userId)
			.OrderBy(l => l.Channel)
			.ToListAsync(ct);

		return new GetNotificationSettingsResponse
		{
			RemindDayBefore = prefs?.RemindDayBefore ?? true,
			RemindShortBefore = prefs?.RemindShortBefore ?? true,
			Channels = links.Select(l => new NotificationChannelResponse
			{
				Channel = l.Channel,
				Address = NotificationExtensions.MaskAddress(l.Address),
				Enabled = l.Enabled,
				LinkedAt = l.LinkedAt
			}).ToList(),
			AvailableChannels = _channels.AvailableChannels
		};
	}
}
