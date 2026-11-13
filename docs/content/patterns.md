---
title: "Patterns"
weight: 22
---

# Patterns

The design patterns this template uses, where each one lives, and where to read more. Modeled on [NimblePros/eShopOnWeb's pattern list](https://github.com/NimblePros/eShopOnWeb/blob/main/docs/explore/patterns.md); the *why* behind each choice is in the [design decisions]({{< relref "design-decisions" >}}) and the ADRs. Paths are under `backend/src/AppTemplate.*`.

## Clean Architecture

Dependencies point inward: SharedKernel ← Core ← UseCases ← Infrastructure ← Web. The domain and the use cases know nothing about EF Core, ASP.NET Core or email; the architecture tests (`LayerDependencyTests`, `ProjectReferenceTests`) fail the build when that slips. [ADR 001]({{< relref "architecture-decisions/adr-001-clean-architecture-layering" >}}), [ardalis/CleanArchitecture](https://github.com/ardalis/CleanArchitecture).

## REPR endpoints

One class per endpoint, with its Request, the Endpoint and its Response together (and its validator in the same file), instead of controllers with many actions: `Web/Endpoints/<Feature>/`, on [FastEndpoints](https://fast-endpoints.com/). [ADR 003]({{< relref "architecture-decisions/adr-003-fastendpoints-mediator" >}}), [DevIQ: REPR](https://deviq.com/design-patterns/repr-design-pattern).

## Mediator, commands and queries

Endpoints send a command or a query (`UseCases/<Feature>/Commands|Queries/`) through [Mediator](https://github.com/martinothamar/Mediator) (source-generated, not MediatR), and one handler answers it. Cross-cutting steps are **pipeline behaviors** around every handler: `LoggingBehavior` and `CachingBehavior` (`Web/Behaviors`). [DevIQ: Mediator](https://deviq.com/design-patterns/mediator-pattern).

## Repository and Specification

Use cases load and save aggregates through a generic `IRepository<T>` (`SharedKernel/IRepository.cs`, implemented by `Infrastructure/Data/EntityFrameworkRepository`). A query that's more than "by id" is a specification class (`Core/Aggregates/UserAggregate/Specifications/UserByIdSpecification`), not a new repository method. Lists that need raw SQL go through **query services** instead (`ListUsersQueryService`). [DevIQ: Repository](https://deviq.com/design-patterns/repository-pattern), [Specification](https://deviq.com/design-patterns/specification-pattern), [Ardalis.Specification](https://github.com/ardalis/Specification).

## Result

Use cases return `Result` / `Result<T>` ([Ardalis.Result](https://github.com/ardalis/Result)) instead of throwing for expected outcomes: `NotFound`, `Invalid`, `Forbidden`, `Conflict`. Endpoints turn them into HTTP responses only through `Web/Extensions/ResultExtensions.cs`, as RFC 9457 problem details. [ADR 009]({{< relref "architecture-decisions/adr-009-problem-details-error-contract" >}}).

## Guard clauses

Methods reject impossible input at their first line, with [Ardalis.GuardClauses](https://github.com/ardalis/GuardClauses) (`Guard.Against.Null`, `Guard.Against.NullOrEmpty`, ...), as `User` and `EmailAddress` do. For a rule checked in several places, add your own `IGuardClause` extension, as eShopOnWeb's `GuardExtensions` does. [DevIQ: Guard clause](https://deviq.com/design-patterns/guard-clause).

## Entities, aggregates and value objects

`User` is an aggregate root (`IAggregateRoot`, `EntityBase<UserId>`): only it changes its state, through methods that enforce its rules. Ids and simple primitives are [Vogen](https://github.com/SteveDunn/Vogen) value objects that validate on construction (`UserId`, `UserName`); compound ones derive from `SharedKernel/ValueObject` (`EmailAddress`). [ADR 005]({{< relref "architecture-decisions/adr-005-vogen-strongly-typed-ids" >}}), [DevIQ: Aggregate](https://deviq.com/domain-driven-design/aggregate-pattern), [Value object](https://deviq.com/domain-driven-design/value-object).

## Smart enums

Closed sets with behavior are [Ardalis.SmartEnum](https://github.com/ardalis/SmartEnum)s, not plain enums or string constants: `Permission` and `Role` (`UseCases/Authorization`; `Role.PermissionsFor`), and `CacheProfile`. [ADR 011]({{< relref "architecture-decisions/adr-011-role-based-permissions" >}}).

## Domain events

An aggregate records what happened (`RegisterDomainEvent`), and handlers react after `SaveChanges` succeeds (`Infrastructure/Data/EventDispatchInterceptor`, `MediatorDomainEventDispatcher`). A handler that needs only Core lives next to its aggregate (`Core/Aggregates/UserAggregate/Handlers`); one that needs a port lives in UseCases. Best-effort and in-process: see [Transactions and side effects]({{< relref "design-decisions#transactions-and-side-effects" >}}). [DevIQ: Domain events](https://deviq.com/design-patterns/domain-events-pattern).

## Ports and adapters

Core and UseCases declare what they need from the outside world as interfaces (**ports**), and Infrastructure implements them (**adapters**):

- `Core/Interfaces/IEmailSender` → `Infrastructure/Email/MimeKitEmailSender`
- `Core/Interfaces/ILoggerAdapter<T>` → `Infrastructure/Logging/LoggerAdapter<T>`, eShopOnWeb's logger adapter: no layer depends on `Microsoft.Extensions.Logging`
- `UseCases/Interfaces/IIdentityService` → `Infrastructure/Identity/IdentityService`, over ASP.NET Core Identity ([ADR 013]({{< relref "architecture-decisions/adr-013-user-management-aspnetcore-identity" >}}))
- `UseCases/Interfaces/ITokenClaimsService` → `Infrastructure/Identity/IdentityTokenClaimService`, eShopOnWeb's token service ([ADR 018]({{< relref "architecture-decisions/adr-018-bearer-tokens-for-non-browser-clients" >}}))

Tests substitute the ports; [DevIQ: Adapter](https://deviq.com/design-patterns/adapter-design-pattern).

## Options, validated at startup

Every setting is a typed options class bound from configuration and validated before the app serves a request (`ValidateOnStart`), so a missing or wrong value stops startup with the setting's name: `Web/Configurations/OptionConfigurations`. A rule too big for a lambda gets an `IValidateOptions<T>` class (`JwtSettingsValidator`, after eShopOnWeb's). [Best practices: Options]({{< relref "best-practices#options" >}}).

## Middleware for cross-cutting request concerns

What applies to every request, regardless of endpoint, is ASP.NET Core middleware in `MiddlewareConfigurations`: forwarded headers, rate limiting, the session's antiforgery check, and `UserContextEnrichmentMiddleware`, which tags each signed-in request's trace and logs with the user's id (after eShopOnWeb's).

## Testing patterns

One test class per member or behavior (`<Type><Member>`), real Postgres through Testcontainers rather than an in-memory fake, functional tests over the real HTTP pipeline, architecture tests for the layer rules, and browser tests only for critical journeys. [ADR 006]({{< relref "architecture-decisions/adr-006-testing-strategy" >}}) has the details and a table of which layer tests what.
