using AppTemplate.ServiceDefaults;
using AppTemplate.Web.Configurations;
using Serilog.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

var serilogLogger = builder.AddLoggerConfigs(); // Serilog to the console
builder.AddServiceDefaults(); // OpenTelemetry (logs, traces, metrics) and health checks

var startupLogger = new SerilogLoggerFactory(serilogLogger).CreateLogger<Program>();

startupLogger.LogInformation("Starting web host");

builder.Services.AddOptionConfigurations(builder.Configuration, startupLogger);
builder.Services.AddServiceConfigurations(startupLogger, builder);
builder.Services.AddAuthenticationConfigurations(startupLogger, builder);
builder.Services.AddAuthorizationConfigurations(startupLogger);

// RFC 9457 problem details (with a traceId) for exceptions, status code pages and TypedResults.Problem.
builder.Services.AddProblemDetails();
builder.Services.AddForwardedHeadersConfigurations(builder.Configuration);
builder.Services.AddRateLimitingConfigurations(builder.Configuration);

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
