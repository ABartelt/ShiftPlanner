using System.Data.Common;
using System.Reflection;
using FastEndpoints;
using FastEndpoints.Swagger;
using Muddi.ShiftPlanner.Server.Api.Extensions;
using Muddi.ShiftPlanner.Server.Api.Services;
using Muddi.ShiftPlanner.Server.Database.Extensions;
using NSwag;
using NSwag.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((context, configuration) => configuration
	.ReadFrom.Configuration(context.Configuration));

builder.Services.AddCors();

var muddiConfig = builder.Configuration.GetRequiredSection("MuddiConnect");
var authBaseUrl = muddiConfig["Authority"]?.TrimEnd('/');
var clientId = muddiConfig["Audience"];
builder.Services.AddFastEndpoints()
	.SwaggerDocument(o =>
	{
		o.EnableJWTBearerAuth = false;
		o.ExcludeNonFastEndpoints = true;
		o.DocumentSettings = ds
			=> ds.AddAuth("muddi-connect", new OpenApiSecurityScheme
			{
				Type = OpenApiSecuritySchemeType.OAuth2,
				Name = "muddi-connect",
				Flows = new OpenApiOAuthFlows()
				{
					AuthorizationCode = new OpenApiOAuthFlow
					{
						AuthorizationUrl = authBaseUrl + "/protocol/openid-connect/auth",
						TokenUrl = authBaseUrl + "/protocol/openid-connect/token"
					}
				}
			});
	});
builder.Services.AddAuthenticationMuddiConnect(builder.Configuration);
builder.Services.AddMuddiShiftPlannerContext(builder.Configuration);
builder.Services.AddDatabaseMigrations();
builder.Services.AddMemoryCache();

builder.Services.AddTransient<ExcelService>();
builder.Services.AddSingleton<NotificationChannelSettings>();

var app = builder.Build();

var assembly = Assembly.GetEntryAssembly()!.GetName();
app.Logger.LogInformation("Start {AssemblyName} v{AssemblyVersion}", assembly.Name, assembly.Version?.ToString(2));

var corsOrigins = builder.Configuration
	.GetSection("Cors")
	.GetSection("Origins")
	.Get<string[]>()?
	.Where(o => !string.IsNullOrWhiteSpace(o))
	.ToArray() ?? [];
if (corsOrigins.Length == 0)
	throw new ArgumentException("You have to set Cors__Origins array in settings");

app.UseCors(b => b.WithOrigins(corsOrigins).AllowAnyMethod().AllowAnyHeader());

app.MapGet("/", () => Results.Redirect("/swagger"));

app.UseAuthentication();
app.UseAuthorization();


app.UseFastEndpoints()
	.UseSwaggerGen(uiConfig: c => c.OAuth2Client = new OAuth2ClientSettings
	{
		ClientId = clientId,
		ClientSecret = string.Empty
	});

try
{
	app.Run();
}
catch (DbException dbexc)
{
	app.Logger.LogCritical("Database error: {Message}", dbexc.Message);
	app.Logger.LogTrace("Debug: {Dbg}", dbexc.ToString());
}