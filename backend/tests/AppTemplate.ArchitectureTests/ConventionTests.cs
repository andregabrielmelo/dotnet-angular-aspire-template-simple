using System.Reflection;
using FastEndpoints;
using NetArchTest.Rules;
using Xunit;

namespace AppTemplate.ArchitectureTests;

/// <summary>Where things go: endpoints in Web's feature folders, handlers in UseCases (ADR 003).</summary>
public class ConventionTests
{
    [Fact]
    public void Endpoints_LiveInWebFeatureFolders()
    {
        var result = Types
            .InAssembly(Layers.Web)
            .That()
            .Inherit(typeof(BaseEndpoint))
            .Should()
            .ResideInNamespaceStartingWith($"{Layers.WebNamespace}.Features.")
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            "Endpoints belong under Web/Features/<Feature>Features/: "
                + string.Join(", ", result.FailingTypeNames ?? [])
        );
    }

    [Fact]
    public void Web_HasNoMvcControllers()
    {
        // FastEndpoints is the only HTTP endpoint style for features.
        var controllers = Layers
            .Web.GetTypes()
            .Where(type => BaseTypes(type).Any(IsMvcController))
            .Select(type => type.FullName)
            .ToList();

        Assert.True(
            controllers.Count == 0,
            $"Use FastEndpoints instead of MVC controllers: {string.Join(", ", controllers)}"
        );
    }

    [Fact]
    public void CommandAndQueryHandlers_LiveInUseCases()
    {
        var misplaced = Layers
            .All.Where(assembly => assembly != Layers.UseCases)
            .SelectMany(assembly => assembly.GetTypes())
            .Where(IsCommandOrQueryHandler)
            .Select(type => type.FullName)
            .ToList();

        Assert.True(
            misplaced.Count == 0,
            $"Command and query handlers belong in UseCases: {string.Join(", ", misplaced)}"
        );
    }

    [Fact]
    public void CommandsAndQueries_AreInTheirOwnFileNamedAfterThem()
    {
        // A record's file can't be read from the assembly, but its name can: every command or
        // query is a <Name>Command or <Name>Query in a UseCases.<Feature>.<UseCase> namespace.
        var badlyNamed = Layers
            .UseCases.GetTypes()
            .Where(IsCommandOrQuery)
            .Where(type =>
                !(type.Name.EndsWith("Command") || type.Name.EndsWith("Query"))
                || type.Namespace?.Split('.').Length < 4
            )
            .Select(type => type.FullName)
            .ToList();

        Assert.True(
            badlyNamed.Count == 0,
            "Commands and queries are named <UseCase>Command/<UseCase>Query and live in "
                + $"UseCases/<Feature>/<UseCase>/: {string.Join(", ", badlyNamed)}"
        );
    }

    private static IEnumerable<Type> BaseTypes(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            yield return current;
        }
    }

    private static bool IsMvcController(Type type) =>
        type.FullName is "Microsoft.AspNetCore.Mvc.ControllerBase";

    private static readonly string[] HandlerInterfaces =
    [
        "Mediator.ICommandHandler`1",
        "Mediator.ICommandHandler`2",
        "Mediator.IQueryHandler`2",
    ];

    private static readonly string[] RequestInterfaces =
    [
        "Mediator.ICommand",
        "Mediator.ICommand`1",
        "Mediator.IQuery`1",
    ];

    private static bool IsCommandOrQueryHandler(Type type) =>
        !type.IsInterface && ImplementsAny(type, HandlerInterfaces);

    private static bool IsCommandOrQuery(Type type) =>
        !type.IsInterface && ImplementsAny(type, RequestInterfaces);

    private static bool ImplementsAny(Type type, string[] interfaceNames) =>
        type.GetInterfaces()
            .Select(i => i.IsGenericType ? i.GetGenericTypeDefinition() : i)
            .Any(i => interfaceNames.Contains(i.FullName));
}
