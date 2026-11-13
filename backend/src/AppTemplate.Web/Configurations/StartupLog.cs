namespace AppTemplate.Web.Configurations;

/// <summary>
/// The startup log lines of the Web host, source-generated (<c>[LoggerMessage]</c>, CA1848), so
/// each one is parsed once and skipped at no cost when its level is off.
/// </summary>
internal static partial class StartupLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Starting web host")]
    public static partial void StartingWebHost(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Project} were configured")]
    public static partial void Configured(ILogger logger, string project);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Project} was configured")]
    public static partial void ConfiguredOne(ILogger logger, string project);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "{Project} were configured ({Permissions} permissions)"
    )]
    public static partial void AuthorizationConfigured(
        ILogger logger,
        string project,
        int permissions
    );

    [LoggerMessage(Level = LogLevel.Information, Message = "{Project} services registered")]
    public static partial void ServicesRegistered(ILogger logger, string project);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Registering Mediator SourceGen and Behaviors"
    )]
    public static partial void RegisteringMediator(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Applying database migrations...")]
    public static partial void ApplyingMigrations(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Pending migration: {Migration}")]
    public static partial void PendingMigration(ILogger logger, string migration);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "An error occurred migrating the DB. {ExceptionMessage}"
    )]
    public static partial void MigrationFailed(
        ILogger logger,
        Exception exception,
        string exceptionMessage
    );

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeding database...")]
    public static partial void SeedingDatabase(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Database seeded successfully")]
    public static partial void DatabaseSeeded(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "An error occurred seeding the DB. {ExceptionMessage}"
    )]
    public static partial void SeedingFailed(
        ILogger logger,
        Exception exception,
        string exceptionMessage
    );
}
