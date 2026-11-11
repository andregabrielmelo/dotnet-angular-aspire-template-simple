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
builder.Services.AddAuthenticationConfigurations(startupLogger);
builder.Services.AddRateLimitingConfigurations(startupLogger);
builder.Services.AddAntiforgeryConfigurations(startupLogger, builder.Environment);

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
            // Generated clients (the Angular types) get non-optional fields for non-nullable ones.
            s.MarkNonNullablePropsAsRequired();
        };
        // No bearer scheme: the reference pages are same-origin, so the browser sends the session
        // cookie set by POST /auth/login.
        o.EnableJWTBearerAuth = false;
    });

var app = builder.Build();

await app.UseAppMiddleware();

// `--export-swagger-docs true` writes v1.json (SwaggerExportPath in the csproj) and exits, before
// anything below needs a database (scripts/export-openapi.sh).
await app.ExportSwaggerDocsAndExitAsync("v1");

await app.StartDatabase(); // decides for itself: Development, or Database:ApplyMigrationsOnStartup

app.MapDefaultEndpoints(); // Aspire health checks and metrics

app.Run();

// Make the implicit Program.cs class public, so integration tests can reference the correct assembly for host building
public partial class Program { }
