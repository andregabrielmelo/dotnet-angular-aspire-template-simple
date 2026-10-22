using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using Serilog;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;
using Serilog.Sinks.OpenTelemetry;

namespace AppTemplate.ServiceDefaults.Logging;

/// <summary>
/// Where log output goes besides the defaults. Production code never sets this; tests use it
/// to capture exactly what the console and OTLP sinks would have written.
/// </summary>
public sealed class LoggingOutputOptions
{
    /// <summary>Replaces the console: the same formatter writes here instead.</summary>
    public TextWriter? ConsoleWriter { get; set; }

    /// <summary>Sends OTLP log exports through this handler (to <see cref="OtlpEndpoint"/>).</summary>
    public HttpMessageHandler? OtlpHandler { get; set; }

    /// <summary>Exports logs here with HTTP/protobuf, instead of <c>OTEL_EXPORTER_OTLP_ENDPOINT</c>.</summary>
    public string? OtlpEndpoint { get; set; }
}

/// <summary>
/// One logging pipeline for every host: Serilog is the only logging provider, and it writes
/// both to the console and, when Aspire (or any OTLP collector) is configured through the
/// standard <c>OTEL_*</c> settings, to OpenTelemetry. Redaction (see
/// <see cref="ClassifiedDataDestructuringPolicy"/> and <see cref="SensitiveDataEnricher"/>)
/// runs before either sink, so both receive the same redacted event.
/// </summary>
public static class LoggingExtensions
{
    public const string ConsoleOutputTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j} {TraceId} {SpanId}{NewLine}{Exception}";

    /// <summary>The formatter the console sink uses (also used when tests capture console output).</summary>
    public static ITextFormatter ConsoleFormatter() =>
        new MessageTemplateTextFormatter(ConsoleOutputTemplate);

    public static TBuilder AddDefaultLogging<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        // Every classified value becomes "[redacted]". Swap in an HMAC redactor here to keep
        // values correlatable across log lines without revealing them.
        builder.Services.AddRedaction(redaction =>
            redaction.SetFallbackRedactor<PlaceholderRedactor>()
        );

        var configuration = builder.Configuration;
        var applicationName = builder.Environment.ApplicationName;

        builder.Services.AddSerilog(
            (services, logger) =>
            {
                var output = services.GetRequiredService<IOptions<LoggingOutputOptions>>().Value;

                logger
                    .MinimumLevel.Information()
                    // Framework noise: request logging below replaces ASP.NET Core's own per-request
                    // lines, EF Core logs every SQL command at Information, and HttpClient logs
                    // full request URLs, query strings included.
                    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                    .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
                    .MinimumLevel.Override("Yarp", LogEventLevel.Warning)
                    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                    // The "Serilog" section can still change levels per environment.
                    .ReadFrom.Configuration(configuration)
                    .Enrich.FromLogContext()
                    .Enrich.WithProperty("Application", applicationName)
                    .Destructure.With(
                        new ClassifiedDataDestructuringPolicy(
                            services.GetRequiredService<IRedactorProvider>()
                        )
                    )
                    // Last enricher, so it also sees properties the ones above added.
                    .Enrich.With<SensitiveDataEnricher>();

                if (output.ConsoleWriter is { } writer)
                {
                    logger.WriteTo.Sink(new TextWriterSink(ConsoleFormatter(), writer));
                }
                else
                {
                    logger.WriteTo.Console(ConsoleFormatter());
                }

                if (output.OtlpEndpoint is { } endpoint)
                {
                    logger.WriteTo.OpenTelemetry(
                        options =>
                        {
                            options.Endpoint = endpoint;
                            options.Protocol = OtlpProtocol.HttpProtobuf;
                            options.HttpMessageHandler = output.OtlpHandler;
                            options.BatchingOptions.BufferingTimeLimit = TimeSpan.FromMilliseconds(
                                100
                            );
                            options.ResourceAttributes = new Dictionary<string, object>
                            {
                                ["service.name"] = applicationName,
                            };
                        },
                        ignoreEnvironment: true
                    );
                }
                else if (!string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
                {
                    // Endpoint, protocol, headers (the Aspire dashboard's API key) and resource
                    // attributes (service name and instance) all come from the OTEL_* settings
                    // Aspire injects, so these logs line up with the traces and metrics.
                    logger.WriteTo.OpenTelemetry(
                        options =>
                        {
                            options.ResourceAttributes = new Dictionary<string, object>
                            {
                                ["service.name"] = applicationName,
                            };
                            // The sink's own export calls must not show up as traced HTTP calls.
                            options.OnBeginSuppressInstrumentation =
                                SuppressInstrumentationScope.Begin;
                        },
                        key => configuration[key]
                    );
                }
            },
            // Never touch the static Log.Logger: several hosts (tests) can run in one process.
            preserveStaticLogger: true
        );

        return builder;
    }

    /// <summary>
    /// One log line per request: method, path (never the query string), status, duration, the
    /// matched endpoint and the caller's <c>sub</c>, correlated by trace and span id. Request
    /// and response bodies and headers (<c>Authorization</c>, <c>Cookie</c>) are never logged.
    /// Health probes log at Verbose, so they don't drown out real traffic.
    /// </summary>
    public static IApplicationBuilder UseDefaultRequestLogging(this IApplicationBuilder app) =>
        app.UseSerilogRequestLogging(options =>
        {
            // This host's logger: the static Log.Logger is deliberately left unconfigured.
            options.Logger = app.ApplicationServices.GetRequiredService<Serilog.ILogger>();
            options.EnrichDiagnosticContext = (diagnostics, context) =>
            {
                diagnostics.Set("EndpointName", context.GetEndpoint()?.DisplayName);
                if (context.User.Identity?.IsAuthenticated == true)
                {
                    diagnostics.Set(
                        "Subject",
                        context.User.FindFirstValue("sub")
                            ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    );
                }
            };
            options.GetLevel = (context, _, exception) =>
                exception is not null || context.Response.StatusCode >= 500 ? LogEventLevel.Error
                : IsHealthProbe(context.Request.Path) ? LogEventLevel.Verbose
                : LogEventLevel.Information;
        });

    private static bool IsHealthProbe(PathString path) =>
        path.StartsWithSegments("/health") || path.StartsWithSegments("/alive");
}

/// <summary>Writes formatted events to a <see cref="TextWriter"/>, one at a time.</summary>
internal sealed class TextWriterSink(ITextFormatter formatter, TextWriter writer)
    : Serilog.Core.ILogEventSink
{
    private readonly Lock _lock = new();

    public void Emit(LogEvent logEvent)
    {
        lock (_lock)
        {
            formatter.Format(logEvent, writer);
            writer.Flush();
        }
    }
}
