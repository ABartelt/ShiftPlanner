using Muddi.ShiftPlanner.Server.Database.Extensions;
using Muddi.ShiftPlanner.Services.Alerting.Channels;
using Muddi.ShiftPlanner.Services.Alerting.Endpoints;
using Muddi.ShiftPlanner.Services.Alerting.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMuddiShiftPlannerContext(builder.Configuration);
builder.Services.AddNotifications(builder.Configuration);

var app = builder.Build();

var channels = app.Services.GetRequiredService<NotificationChannelRegistry>().Available;
app.Logger.LogInformation("Reminder channels: {Channels}", channels.Count == 0 ? "none" : string.Join(", ", channels));

app.MapGet("/health", () => Results.Ok());
app.MapEmailActionEndpoints();
if (builder.Configuration.IsTwilioConfigured())
	app.MapTwilioWebhook();

app.Run();
