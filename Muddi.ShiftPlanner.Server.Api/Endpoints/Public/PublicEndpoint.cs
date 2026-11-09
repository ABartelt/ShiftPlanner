using FastEndpoints;
using Muddi.ShiftPlanner.Server.Api.Services;
using Muddi.ShiftPlanner.Server.Database.Contexts;
using Muddi.ShiftPlanner.Server.Database.Entities;

namespace Muddi.ShiftPlanner.Server.Api.Endpoints.Public;

/// <summary>
/// Base for the anonymous <c>/public/*</c> read endpoints. Deliberately NOT derived from <see cref="CrudEndpoint{TRequest,TResponse}"/>:
/// that family defaults to admin-only, this one is always anonymous. The base handles the gating so a concrete
/// endpoint only contains its query:
/// <list type="bullet">
/// <item>404 when <see cref="Shared.Entities.ApplicationSettings.PublicBrowsingEnabled"/> is off (the feature is "not deployed here")</item>
/// <item>404 when no season exists; otherwise the current season is passed in, callers never choose a season</item>
/// <item>404 when the query returns null (not found / not visible)</item>
/// </list>
/// Implementations must call the <see cref="ScheduleQueries"/> helpers with <see cref="ScheduleAccess.Anonymous"/>
/// and must never touch <c>User</c>. Routes must start with <c>/public/</c> (enforced by an architecture test).
/// </summary>
public abstract class PublicEndpoint<TRequest, TResponse> : Endpoint<TRequest, TResponse>
	where TRequest : notnull
	where TResponse : notnull
{
	/// <summary>Requests per minute per client IP. Generous, because many volunteers may share one venue IP.</summary>
	public const int ThrottleHitLimit = 600;

	protected PublicEndpoint(ShiftPlannerContext database, IAppSettingsProvider settings)
	{
		Database = database;
		Settings = settings;
	}

	protected ShiftPlannerContext Database { get; }
	protected IAppSettingsProvider Settings { get; }

	/// <summary>Only the route (<c>Get("/public/...")</c>). Everything else is configured here.</summary>
	protected abstract void PublicConfigure();

	/// <summary>Run the query for the current season. Return null for "not found".</summary>
	protected abstract Task<TResponse?> ExecutePublicAsync(TRequest req, SeasonEntity season, CancellationToken ct);

	public sealed override void Configure()
	{
		PublicConfigure();
		AllowAnonymous();
		Throttle(ThrottleHitLimit, 60);
		Description(d => d
			.WithTags("Public")
			.Produces<TResponse>()
			.Produces(StatusCodes.Status404NotFound));
	}

	public sealed override async Task HandleAsync(TRequest req, CancellationToken ct)
	{
		var settings = await Settings.GetAsync(ct);
		if (!settings.PublicBrowsingEnabled)
		{
			await Send.NotFoundAsync(ct);
			return;
		}

		var season = await Database.GetCurrentSeasonAsync(ct);
		if (season is null)
		{
			await Send.NotFoundAsync(ct);
			return;
		}

		var result = await ExecutePublicAsync(req, season, ct);
		if (result is null)
		{
			await Send.NotFoundAsync(ct);
			return;
		}

		Response = result;
	}
}

public abstract class PublicEndpointWithoutRequest<TResponse> : PublicEndpoint<EmptyRequest, TResponse>
	where TResponse : notnull
{
	protected PublicEndpointWithoutRequest(ShiftPlannerContext database, IAppSettingsProvider settings)
		: base(database, settings)
	{
	}
}
