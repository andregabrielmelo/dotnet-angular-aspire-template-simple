using AppTemplate.ServiceDefaults;
using AppTemplate.Web.Configurations;

var builder = WebApplication.CreateBuilder(args);

builder.AddLoggerConfigs() // Serilog to the console
    .AddServiceDefaults(); // OpenTelemetry (logs, traces, metrics) and health checks

using var loggerFactory = LoggerFactory.Create(config => config.AddConsole());
var startupLogger = loggerFactory.CreateLogger<Program>();

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
    });

// TODO: Add response compression if needed. Prefer server-based response compression technologies, probably better.
// https://learn.microsoft.com/en-us/aspnet/core/performance/response-compression?view=aspnetcore-10.0
//builder.Services.AddResponseCompression(options =>
//{
//    options.EnableForHttps = true;
//    options.Providers.Add<BrotliCompressionProvider>();
//    options.Providers.Add<GzipCompressionProvider>();
//});

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
