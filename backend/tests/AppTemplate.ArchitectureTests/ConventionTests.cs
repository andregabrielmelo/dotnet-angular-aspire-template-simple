using System.Text.RegularExpressions;
using FastEndpoints;
using NetArchTest.Rules;
using Xunit;

namespace AppTemplate.ArchitectureTests;

/// <summary>
/// Where things go: endpoints in Web's feature namespaces, command and query handlers in UseCases
/// (ADR 003), domain event handlers in Core.
/// </summary>
public class ConventionTests
{
    [Fact]
    public void Endpoints_LiveInWebFeatureNamespaces()
    {
        // Web/Endpoints/<Feature>/ uses the namespace AppTemplate.Web.<Feature>, never one of
        // Web's infrastructure namespaces.
        var result = Types
            .InAssembly(Layers.Web)
            .That()
            .Inherit(typeof(BaseEndpoint))
            .ShouldNot()
            .ResideInNamespaceMatching(
                $@"^{Regex.Escape(Layers.WebNamespace)}(\.(Configurations|Extensions|Behaviors|Authorization))?$"
            )
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            "Endpoints belong under Web/Endpoints/<Feature>/ (namespace AppTemplate.Web.<Feature>): "
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
    public void DomainEventHandlers_LiveInCore()
    {
        // A domain event handler sits next to its aggregate and uses only Core. Work that needs a
        // UseCases port (sending an account email, say) belongs in the use case itself, as
        // RegisterUserHandler does.
        var misplaced = Layers
            .All.Where(assembly => assembly != Layers.Core)
            .SelectMany(assembly => assembly.GetTypes())
            .Where(IsDomainEventHandler)
            .Select(type => type.FullName)
            .ToList();

        Assert.True(
            misplaced.Count == 0,
            "Domain event handlers belong in Core (Core/Aggregates/<Aggregate>/Handlers/): "
                + string.Join(", ", misplaced)
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
                !(
                    type.Name.EndsWith("Command", StringComparison.Ordinal)
                    || type.Name.EndsWith("Query", StringComparison.Ordinal)
                )
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

    private static bool IsDomainEventHandler(Type type) =>
        !type.IsInterface
        && !type.IsGenericTypeDefinition
        && type.GetInterfaces()
            .Any(i =>
                i.IsGenericType
                && i.GetGenericTypeDefinition().FullName == "Mediator.INotificationHandler`1"
                && typeof(SharedKernel.IDomainEvent).IsAssignableFrom(i.GetGenericArguments()[0])
            );

    private static bool IsCommandOrQuery(Type type) =>
        !type.IsInterface && ImplementsAny(type, RequestInterfaces);

    private static bool ImplementsAny(Type type, string[] interfaceNames) =>
        type.GetInterfaces()
            .Select(i => i.IsGenericType ? i.GetGenericTypeDefinition() : i)
            .Any(i => interfaceNames.Contains(i.FullName));
}
