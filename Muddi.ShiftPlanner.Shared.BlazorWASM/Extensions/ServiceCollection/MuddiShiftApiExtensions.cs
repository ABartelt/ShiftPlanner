using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Muddi.ShiftPlanner.Shared.Api;
using Refit;

namespace Muddi.ShiftPlanner.Shared.BlazorWASM;

public static partial class ServiceCollectionExtensions
{
	public static void AddShiftApiExtensions(this IServiceCollection services, IConfigurationSection configuration)
	{
		var baseUri = configuration["BaseUrl"] ??
		              throw new ArgumentNullException(nameof(configuration), "Failed to get BaseUrl from configuration");
		services.AddScoped<AuthorizationMessageHandler>();
		var settings = new RefitSettings
		{
			UrlParameterFormatter = new CustomUrlParameterFormatter()
		};
		services.AddRefitClient<IMuddiShiftApi>(settings)
			.ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUri))
			.AddHttpMessageHandler(sp => sp
				.GetRequiredService<AuthorizationMessageHandler>()
				.ConfigureHandler(authorizedUrls: new[] { baseUri }));
		// Anonymous surface: deliberately WITHOUT the AuthorizationMessageHandler, which would throw
		// AccessTokenNotAvailableException for every call when nobody is logged in.
		services.AddRefitClient<IMuddiPublicShiftApi>(settings)
			.ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUri));
#if !DEBUG
		services.RemoveAll<IHttpMessageHandlerBuilderFilter>();
#endif
	}
}