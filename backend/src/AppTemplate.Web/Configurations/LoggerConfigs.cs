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
    /// <returns>The builder. The console logger is also <see cref="Log.Logger"/>, which writes the
    /// startup log lines before the host is built.</returns>
    public static WebApplicationBuilder AddLoggerConfigs(this WebApplicationBuilder builder)
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", builder.Environment.ApplicationName)
            .WriteTo.Console()
            .CreateLogger();

        // Called before AddServiceDefaults, so clearing the default providers (whose console
        // would write every line a second time) keeps OpenTelemetry's.
        builder.Logging.ClearProviders().AddSerilog(Log.Logger, dispose: true);

        return builder;
    }
}
