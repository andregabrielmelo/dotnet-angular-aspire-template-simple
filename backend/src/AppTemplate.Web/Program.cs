using AppTemplate.Infrastructure.Logging;
using AppTemplate.ServiceDefaults;
using AppTemplate.Web.Configurations;
using Serilog;
using Serilog.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

builder
    .AddLoggerConfigs() // Serilog to the console
    .AddServiceDefaults(); // OpenTelemetry (logs, traces, metrics) and health checks

// The host's loggers don't exist until it is built, so startup logs through the console logger.
using var startupLoggerFactory = new SerilogLoggerFactory(Log.Logger);
var startupLogger = new LoggerAdapter<Program>(startupLoggerFactory);

// A failure while building the host or preparing the database (a missing connection string, a
// failed migration) is logged with its exception, then rethrown, so the process still exits with an
// error. Failures once the host starts (an invalid setting found by ValidateOnStart) are logged by
// the host itself, as "Hosting failed to start". dotnet ef throws HostAbortedException on purpose
// to stop the host once it has the model; that isn't a failure.
WebApplication app;
try
{
    startupLogger.LogInformation("Starting web host");

    builder.Services.AddOptionConfigurations(builder.Configuration, startupLogger);
    builder.Services.AddServiceConfigurations(startupLogger, builder);
    builder.Services.AddAuthenticationConfigurations(startupLogger);
    builder.Services.AddRateLimitingConfigurations(startupLogger);
    builder.Services.AddAntiforgeryConfigurations(startupLogger);

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
            // The document declares the bearer scheme (POST /auth/token, ADR 018); the reference pages
            // themselves are same-origin, so the browser also sends the session cookie.
        });

    // TODO: Add response compression if needed. Prefer server-based response compression technologies, probably better.
    // https://learn.microsoft.com/en-us/aspnet/core/performance/response-compression?view=aspnetcore-10.0
    //builder.Services.AddResponseCompression(options =>
    //{
    //    options.EnableForHttps = true;
    //    options.Providers.Add<BrotliCompressionProvider>();
    //    options.Providers.Add<GzipCompressionProvider>();
    //});

    app = builder.Build();

    await app.UseAppMiddleware();

    // `--export-swagger-docs true` writes v1.json (SwaggerExportPath in the csproj) and exits, before
    // anything below needs a database (scripts/export-openapi.sh).
    await app.ExportSwaggerDocsAndExitAsync("v1");

    await app.StartDatabase(); // decides for itself: Development, or Database:ApplyMigrationsOnStartup

    app.MapDefaultEndpoints(); // Aspire health checks and metrics
}
catch (Exception exception) when (exception is not HostAbortedException)
{
    startupLogger.LogError(exception, "The web host failed to start");
    // No host disposes the console logger on this path, so flush it before the process exits.
    await Log.CloseAndFlushAsync();
    throw;
}

app.Run();

// Make the implicit Program.cs class public, so integration tests can reference the correct assembly for host building
public partial class Program { }
