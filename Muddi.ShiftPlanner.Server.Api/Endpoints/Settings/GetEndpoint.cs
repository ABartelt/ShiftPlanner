using FastEndpoints;
using Muddi.ShiftPlanner.Server.Api.Services;

namespace Muddi.ShiftPlanner.Server.Api.Endpoints.Settings;

/// <summary>
/// Returns app-wide settings (Title, Subtitle, Contact, StartTime, EndTime, Greeting, MemberName, PublicBrowsingEnabled)
/// stored in the database. If no settings exist in the DB, returns defaults.
/// </summary>
public class GetEndpoint : EndpointWithoutRequest<GetAppSettingsResponse>
{
	private readonly IAppSettingsProvider _settings;

	public GetEndpoint(IAppSettingsProvider settings)
	{
		_settings = settings;
	}

	public override void Configure()
	{
		Get("/settings");
	}

	public override async Task<GetAppSettingsResponse> ExecuteAsync(CancellationToken ct)
	{
		return new GetAppSettingsResponse
		{
			Settings = await _settings.GetAsync(ct)
		};
	}
}
