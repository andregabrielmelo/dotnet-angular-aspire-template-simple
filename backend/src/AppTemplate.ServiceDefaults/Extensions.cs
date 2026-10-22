using AppTemplate.ServiceDefaults.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace AppTemplate.ServiceDefaults;

// Adds common Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/dotnet/aspire/service-defaults
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.AddDefaultLogging();

        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            http.AddStandardResilienceHandler();

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        // Uncomment the following to restrict the allowed schemes for service discovery.
        // builder.Services.Configure<ServiceDiscoveryOptions>(options =>
        // {
        //     options.AllowedSchemes = ["https"];
        // });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        // Logs go through Serilog (AddDefaultLogging), which exports them over OTLP itself
        // after redaction, so OpenTelemetry's own logging provider isn't added here.
        builder
            .Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    // Connection pool and command metrics; Npgsql emits them itself.
                    .AddMeter("Npgsql")
                    // EF Core's own metrics (queries, SaveChanges).
                    .AddMeter("Microsoft.EntityFrameworkCore");
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(builder.Environment.ApplicationName)
                    .AddAspNetCoreInstrumentation(tracing =>
                        // Exclude health check requests from tracing
                        tracing.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                            && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath)
                    )
                    // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
                    //.AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation()
                    // A span per SQL command; Npgsql emits them itself.
                    .AddSource("Npgsql");
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(
            builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
        );

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        // Uncomment the following lines to enable the Azure Monitor exporter (requires the Azure.Monitor.OpenTelemetry.AspNetCore package)
        //if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        //{
        //    builder.Services.AddOpenTelemetry()
        //       .UseAzureMonitor();
        //}

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder
            .Services.AddHealthChecks()
            // The process is up and responsive: nothing else belongs in liveness, or a
            // database outage would get every instance restarted.
            .AddCheck("self", () => HealthCheckResult.Healthy(), [HealthCheckTags.Live]);

        return builder;
    }

    /// <summary>
    /// Two probes, mapped in every environment. They are anonymous, excluded from rate
    /// limiting and tracing, and answer with only the status word, so they reveal nothing about
    /// the dependencies themselves:
    /// <list type="bullet">
    /// <item><c>/alive</c> (liveness): the process is up. Failing means "restart me".</item>
    /// <item><c>/health</c> (readiness): every required dependency (<see cref="HealthCheckTags.Ready"/>,
    /// such as Postgres) is reachable. Failing (503) means "send no traffic here".</item>
    /// </list>
    /// </summary>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks(
                AlivenessEndpointPath,
                new HealthCheckOptions { Predicate = r => r.Tags.Contains(HealthCheckTags.Live) }
            )
            .AllowAnonymous();

        app.MapHealthChecks(
                HealthEndpointPath,
                new HealthCheckOptions { Predicate = r => r.Tags.Contains(HealthCheckTags.Ready) }
            )
            .AllowAnonymous();

        return app;
    }
}

/// <summary>Which probe a health check belongs to (see <see cref="Extensions.MapDefaultEndpoints"/>).</summary>
public static class HealthCheckTags
{
    /// <summary>Liveness: only checks of the process itself.</summary>
    public const string Live = "live";

    /// <summary>Readiness: a required dependency. Unhealthy takes the instance out of rotation.</summary>
    public const string Ready = "ready";
}
