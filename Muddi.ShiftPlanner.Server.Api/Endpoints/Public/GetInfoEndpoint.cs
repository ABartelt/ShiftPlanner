using FastEndpoints;
using Mapster;
using Muddi.ShiftPlanner.Server.Api.Services;
using Muddi.ShiftPlanner.Server.Database.Contexts;
using Muddi.ShiftPlanner.Shared.Contracts.v1.Responses.Seasons;

namespace Muddi.ShiftPlanner.Server.Api.Endpoints.Public;

/// <summary>
/// The one public endpoint that is reachable even when public browsing is disabled: it tells the client
/// whether it is. Branding and current season are only included when enabled.
/// </summary>
public sealed class GetInfoEndpoint : EndpointWithoutRequest<GetPublicInfoResponse>
{
	private readonly ShiftPlannerContext _database;
	private readonly IAppSettingsProvider _settings;

	public GetInfoEndpoint(ShiftPlannerContext database, IAppSettingsProvider settings)
	{
		_database = database;
		_settings = settings;
	}

	public override void Configure()
	{
		Get("/public/info");
		AllowAnonymous();
		Throttle(PublicEndpoint<EmptyRequest, GetPublicInfoResponse>.ThrottleHitLimit, 60);
		Description(d => d.WithTags("Public"));
	}

	public override async Task<GetPublicInfoResponse> ExecuteAsync(CancellationToken ct)
	{
		var settings = await _settings.GetAsync(ct);
		if (!settings.PublicBrowsingEnabled)
			return new GetPublicInfoResponse { PublicBrowsingEnabled = false };

		var season = await _database.GetCurrentSeasonAsync(ct);
		return new GetPublicInfoResponse
		{
			PublicBrowsingEnabled = true,
			Settings = settings,
			CurrentSeason = season?.Adapt<GetSeasonResponse>()
		};
	}
}
