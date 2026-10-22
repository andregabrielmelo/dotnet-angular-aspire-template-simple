using System.Diagnostics.Metrics;

namespace AppTemplate.UseCases.Telemetry;

/// <summary>
/// Business metrics, exported over OpenTelemetry with the rest (ServiceDefaults adds every
/// "AppTemplate.*" meter): what the application did, as opposed to how the runtime behaved.
/// Each counter is incremented where the thing has actually happened, never where it was only
/// requested. Tags must stay bounded: never a user id, email or other per-user value, because
/// every distinct tag value is a new time series.
/// </summary>
public sealed class ApplicationMetrics
{
    public const string MeterName = "AppTemplate.Application";

    private readonly Counter<long> _usersProvisioned;

    public ApplicationMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _usersProvisioned = meter.CreateCounter<long>(
            "users.provisioned",
            unit: "{user}",
            description: "Users created on their first sign-in."
        );
    }

    public void UserProvisioned() => _usersProvisioned.Add(1);
}
