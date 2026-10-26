using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Muddi.ShiftPlanner.Server.Database.Contexts;
using Muddi.ShiftPlanner.Shared.Entities;

namespace Muddi.ShiftPlanner.Server.Api.Services;

/// <summary>
/// Cached read access to the single <see cref="ApplicationSettings"/> row. Used on every public request to
/// check <see cref="ApplicationSettings.PublicBrowsingEnabled"/>, so it must not hit the database each time.
/// </summary>
public interface IAppSettingsProvider
{
	ValueTask<ApplicationSettings> GetAsync(CancellationToken ct);

	/// <summary>Drop the cached copy, e.g. after the super-admin saved new settings.</summary>
	void Invalidate();
}

public sealed class AppSettingsProvider(ShiftPlannerContext database, IMemoryCache cache) : IAppSettingsProvider
{
	private const string CacheKey = "app-settings";

	/// <summary>Upper bound for how long a flag change may take to propagate (relevant with several API instances).</summary>
	public static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

	public async ValueTask<ApplicationSettings> GetAsync(CancellationToken ct)
	{
		if (cache.TryGetValue(CacheKey, out ApplicationSettings? cached) && cached is not null)
			return cached;

		var entity = await database.AppSettings.AsNoTracking().FirstOrDefaultAsync(ct);
		var settings = entity?.Settings ?? new ApplicationSettings();
		cache.Set(CacheKey, settings, CacheDuration);
		return settings;
	}

	public void Invalidate() => cache.Remove(CacheKey);
}
