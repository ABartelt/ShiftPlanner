using Muddi.ShiftPlanner.Services.Alerting.Channels;
using Muddi.ShiftPlanner.Services.Alerting.Inbound;
using Muddi.ShiftPlanner.Services.Alerting.Notifications;
using Muddi.ShiftPlanner.Services.Alerting.Options;
using Muddi.ShiftPlanner.Services.Alerting.Services;
using Telegram.Bot;
using Twilio.Clients;

namespace Muddi.ShiftPlanner.Services.Alerting.Extensions;

public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Reminders plus every messenger whose configuration section is complete.
	/// </summary>
	public static void AddNotifications(this IServiceCollection services, IConfiguration configuration)
	{
		services.Configure<NotificationOptions>(configuration.GetSection("Notifications"));
		services.AddSingleton(TimeProvider.System);
		services.AddSingleton<ShiftFormatter>();
		services.AddSingleton<NotificationChannelRegistry>();
		services.AddScoped<ReminderDispatcher>();
		services.AddScoped<ShiftReleaseService>();
		services.AddScoped<InboundCommandHandler>();
		services.AddHostedService<ReminderSchedulerService>();

		services.AddTelegramBot(configuration);
		services.AddWhatsApp(configuration);
		services.AddSignal(configuration);
		services.AddEmail(configuration);
	}

	private static void AddTelegramBot(this IServiceCollection services, IConfiguration configuration)
	{
		var section = configuration.GetSection("Telegram");
		services.Configure<TelegramOptions>(section);
		var options = section.Get<TelegramOptions>() ?? new();
		if (!options.IsConfigured)
			return;

		services.AddSingleton<ITelegramBotClient>(_ => new TelegramBotClient(options.ApiToken));
		services.AddSingleton<TelegramChannel>();
		services.AddSingleton<INotificationChannel>(sp => sp.GetRequiredService<TelegramChannel>());
		services.AddHostedService<TelegramUpdateReceiver>();
		if (options.GroupId != 0)
			services.AddHostedService<AutomaticShiftCheckingService>();
	}

	private static void AddWhatsApp(this IServiceCollection services, IConfiguration configuration)
	{
		var section = configuration.GetSection("Twilio");
		services.Configure<TwilioOptions>(section);
		var options = section.Get<TwilioOptions>() ?? new();
		if (!options.IsConfigured)
			return;

		services.AddSingleton<ITwilioRestClient>(_ => new TwilioRestClient(options.AccountSid, options.AuthToken));
		services.AddSingleton<INotificationChannel, TwilioWhatsAppChannel>();
	}

	private static void AddSignal(this IServiceCollection services, IConfiguration configuration)
	{
		var section = configuration.GetSection("Signal");
		services.Configure<SignalOptions>(section);
		var options = section.Get<SignalOptions>() ?? new();
		if (!options.IsConfigured)
			return;

		services.AddHttpClient(SignalChannel.HttpClientName,
			c => c.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/"));
		services.AddSingleton<SignalChannel>();
		services.AddSingleton<INotificationChannel>(sp => sp.GetRequiredService<SignalChannel>());
		services.AddHostedService<SignalReceiveService>();
	}

	private static void AddEmail(this IServiceCollection services, IConfiguration configuration)
	{
		var section = configuration.GetSection("Smtp");
		services.Configure<SmtpOptions>(section);
		var options = section.Get<SmtpOptions>() ?? new();
		if (!options.IsConfigured)
			return;

		services.AddSingleton<INotificationChannel, EmailChannel>();
	}

	public static bool IsTwilioConfigured(this IConfiguration configuration)
		=> configuration.GetSection("Twilio").Get<TwilioOptions>()?.IsConfigured == true;
}
