using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace AppTemplate.ArchitectureTests;

/// <summary>
/// Inspects the compiled code: a layer may not use types from an outer layer or from a
/// framework that belongs to one (ADR 001). These catch dependencies that arrive
/// transitively, which <see cref="ProjectReferenceTests"/> can't see.
/// </summary>
public class LayerDependencyTests
{
    [Fact]
    public void SharedKernel_DependsOnNoOtherLayer() =>
        AssertNoDependencies(
            Layers.SharedKernel,
            Layers.CoreNamespace,
            Layers.UseCasesNamespace,
            Layers.InfrastructureNamespace,
            Layers.WebNamespace
        );

    [Fact]
    public void Core_DependsOnlyOnSharedKernel() =>
        AssertNoDependencies(
            Layers.Core,
            Layers.UseCasesNamespace,
            Layers.InfrastructureNamespace,
            Layers.WebNamespace,
            "Microsoft.EntityFrameworkCore",
            "Microsoft.AspNetCore"
        );

    [Fact]
    public void UseCases_DependOnNeitherInfrastructureNorWeb() =>
        AssertNoDependencies(
            Layers.UseCases,
            Layers.InfrastructureNamespace,
            Layers.WebNamespace,
            "Microsoft.EntityFrameworkCore",
            "Npgsql",
            "Microsoft.AspNetCore",
            "MailKit",
            "StackExchange.Redis"
        );

    [Fact]
    public void Infrastructure_DoesNotDependOnWeb() =>
        AssertNoDependencies(Layers.Infrastructure, Layers.WebNamespace);

    [Fact]
    public void NoLayer_UsesMediatR()
    {
        // Mediator (martinothamar) is source-generated; MediatR would be a second dispatcher.
        foreach (var assembly in Layers.All)
        {
            AssertNoDependencies(assembly, "MediatR");
        }
    }

    private static void AssertNoDependencies(Assembly assembly, params string[] forbidden)
    {
        var result = Types
            .InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAny(forbidden)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            $"{assembly.GetName().Name} must not depend on {string.Join(", ", forbidden)}. "
                + $"Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}"
        );
    }
}
