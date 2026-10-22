using AppTemplate.ServiceDefaults;
using AppTemplate.Web.Configurations;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults(); // OpenTelemetry, health checks, and Serilog logging with redaction

using var loggerFactory = LoggerFactory.Create(config => config.AddConsole());
var startupLogger = loggerFactory.CreateLogger<Program>();

startupLogger.LogInformation("Starting web host");

builder.Services.AddOptionConfigurations(builder.Configuration, startupLogger, builder);
builder.Services.AddServiceConfigurations(startupLogger, builder);
builder.Services.AddAuthenticationConfigurations(startupLogger, builder);
builder.Services.AddAuthorizationConfigurations(startupLogger, builder);
builder.Services.AddProblemDetailsConfigurations(startupLogger);
builder.Services.AddForwardedHeadersConfigurations(builder.Configuration);
builder.Services.AddRateLimitingConfigurations(builder.Configuration);
builder.Services.AddRequestTimeoutConfigurations(builder.Configuration);

builder
    .Services.AddFastEndpoints()
    .SwaggerDocument(o =>
    {
        o.ShortSchemaNames = true;
        o.DocumentSettings = s =>
        {
            s.Title = "AppTemplate API";
            s.Version = "v1";
            s.Description = "REST API for AppTemplate.";
            s.SchemaSettings.SchemaProcessors.Add(
                new RequireNonNullablePropertiesSchemaProcessor()
            );
        };
    });

var app = builder.Build();

await app.UseAppMiddleware();

// `--OpenApi:ExportPath=<file>` writes the OpenAPI document and exits, before anything below
// needs a database or other services (see OpenApiExport).
if (await app.TryExportOpenApiAsync())
{
    return;
}
await app.StartDatabase(); // decides for itself: Development, or Database:ApplyMigrationsOnStartup

app.MapDefaultEndpoints(); // Aspire health checks and metrics

app.Run();

// Make the implicit Program.cs class public, so integration tests can reference the correct assembly for host building
public partial class Program { }
