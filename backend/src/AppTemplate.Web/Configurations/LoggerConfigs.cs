using System.Globalization;
using Serilog;

namespace AppTemplate.Web.Configurations;

public static class LoggerConfigs
{
    /// <summary>
    /// Serilog writes the console, alongside OpenTelemetry's provider (added by
    /// <c>AddServiceDefaults</c>), which exports the same logs to the Aspire dashboard. The
    /// console's levels come from the <c>Serilog</c> section (Serilog ignores <c>Logging</c>),
    /// the dashboard's from <c>Logging</c>; keep the two in step.
    /// Requests carry passwords and tokens, so log names and ids, never request bodies or DTOs.
    /// </summary>
    /// <returns>The logger, for the startup log lines written before the host is built.</returns>
    public static Serilog.ILogger AddLoggerConfigs(this WebApplicationBuilder builder)
    {
        var logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", builder.Environment.ApplicationName)
            .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
            .CreateLogger();

        // Called before AddServiceDefaults, so clearing the defaults keeps OpenTelemetry's provider.
        builder.Logging.ClearProviders().AddSerilog(logger, dispose: true);

        return logger;
    }
}
