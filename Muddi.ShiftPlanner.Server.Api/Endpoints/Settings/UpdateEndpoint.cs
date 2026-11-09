using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Muddi.ShiftPlanner.Server.Api.Services;
using Muddi.ShiftPlanner.Server.Database.Contexts;
using Muddi.ShiftPlanner.Server.Database.Entities;
using Muddi.ShiftPlanner.Shared.Entities;

namespace Muddi.ShiftPlanner.Server.Api.Endpoints.Settings;

/// <summary>
/// Updates app-wide settings (Title, Subtitle, Contact, StartTime, EndTime, Greeting, MemberName).
/// Admin-only endpoint. Creates the settings row if it doesn't exist yet.
/// </summary>
public class UpdateEndpoint : CrudEndpoint<UpdateAppSettingsRequest, EmptyResponse>
{
	private readonly IAppSettingsProvider _settingsProvider;

	public UpdateEndpoint(ShiftPlannerContext database, IAppSettingsProvider settingsProvider) : base(database)
	{
		_settingsProvider = settingsProvider;
	}

	protected override void CrudConfigure()
	{
		Put("/settings");
		Roles(ApiRoles.SuperAdmin);
	}

	public override async Task<EmptyResponse> ExecuteAsync(UpdateAppSettingsRequest req, CancellationToken ct)
	{
		var entity = await Database.AppSettings.FirstOrDefaultAsync(ct);

		var settings = new ApplicationSettings
		{
			Title = req.Title,
			Subtitle = req.Subtitle,
			Contact = req.Contact,
			StartTime = req.StartTime,
			EndTime = req.EndTime,
			Greeting = req.Greeting,
			MemberName = req.MemberName,
			PublicBrowsingEnabled = req.PublicBrowsingEnabled
		};

		if (entity is null)
		{
			// First time — insert the row
			entity = new AppSettingsEntity { Settings = settings };
			Database.AppSettings.Add(entity);
		}
		else
		{
			entity.Settings = settings;
		}

		await Database.SaveChangesAsync(ct);
		_settingsProvider.Invalidate();
		return EmptyResponse.Instance;
	}
}