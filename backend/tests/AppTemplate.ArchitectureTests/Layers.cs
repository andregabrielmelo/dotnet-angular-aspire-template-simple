using System.Reflection;

namespace AppTemplate.ArchitectureTests;

/// <summary>The solution's layers, as compiled assemblies and as namespace prefixes.</summary>
internal static class Layers
{
    public const string SharedKernelNamespace = "AppTemplate.SharedKernel";
    public const string CoreNamespace = "AppTemplate.Core";
    public const string UseCasesNamespace = "AppTemplate.UseCases";
    public const string InfrastructureNamespace = "AppTemplate.Infrastructure";
    public const string WebNamespace = "AppTemplate.Web";

    public static readonly Assembly SharedKernel = typeof(SharedKernel.EntityBase).Assembly;
    public static readonly Assembly Core = typeof(Core.Aggregates.UserAggregate.User).Assembly;
    public static readonly Assembly UseCases = typeof(UseCases.PagedResult<>).Assembly;
    public static readonly Assembly Infrastructure =
        typeof(Infrastructure.InfrastructureServiceExtensions).Assembly;
    public static readonly Assembly Web = typeof(Program).Assembly;

    public static readonly Assembly[] All = [SharedKernel, Core, UseCases, Infrastructure, Web];
}
