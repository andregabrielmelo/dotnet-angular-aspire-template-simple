---
title: "Goals & Design Decisions"
weight: 20
---

# Goals

This template's goal is to give a well-factored starting point for a full-stack project - a .NET backend following Clean Architecture, an Angular frontend, and .NET Aspire tying local development together - without being a real "sample app." The `User` feature exists to show you where things belong; replace it with your own domain as soon as you understand the shape.

Learn more about the underlying architecture:

- [Domain-Driven Design Fundamentals](https://www.pluralsight.com/courses/fundamentals-domain-driven-design)
- [SOLID Principles for C# Developers](https://www.pluralsight.com/courses/csharp-solid-principles)

This template's Clean Architecture layout is itself based on [Ardalis' Clean Architecture template](https://github.com/ardalis/CleanArchitecture) - see that project's own [design decisions](https://github.com/ardalis/CleanArchitecture/blob/main/docs/content/design-decisions.md) for a deeper treatment of the layering.

# Where To Validate

Validation happens in two places by design:

- **Web** - FastEndpoints + FluentValidation validate the shape of incoming requests (REPR pattern: Request-Endpoint-Response).
- **Core** - domain entities and value objects (e.g. `UserName`, `EmailAddress`, both [Vogen](https://github.com/SteveDunn/Vogen) value objects) enforce invariants via their own `Validate` methods, so they can never exist in an invalid state regardless of caller.

This is some duplication, but it's cheap and it means the domain model stays safe even if a future caller isn't an HTTP endpoint (a background job, a console tool, a test).

# Layers

## Core

Domain model only: aggregates, value objects, domain events, specifications, and interfaces that Infrastructure implements. Almost no external dependencies - just [Ardalis.Specification](https://github.com/ardalis/Specification) and [Vogen](https://github.com/SteveDunn/Vogen) for strongly-typed IDs.

## UseCases

CQRS commands/queries via [Mediator](https://github.com/martinothamar/Mediator) (a compile-time source-generated alternative to MediatR). Depends on Core, not on Infrastructure - data access is expressed through `IRepository<T>` and query-service interfaces defined here and implemented in Infrastructure.

## Infrastructure

EF Core + Npgsql implementation of the repositories and query services, plus anything else that talks to the outside world (email via MailKit, etc.). Implements interfaces defined in Core/UseCases so nothing above it depends on EF Core directly.

## Web

The ASP.NET Core entry point. [FastEndpoints](https://fast-endpoints.com/) + the REPR pattern for one-file-per-endpoint API design, [Scalar](https://github.com/scalar/scalar) for interactive API docs, [Serilog](https://serilog.net/) for console logging (`LoggerConfigs`), next to OpenTelemetry's export to the Aspire dashboard, and ASP.NET Core Identity's cookie for sessions.

## SharedKernel

Small cross-cutting pieces shared by every layer above Core (base entity/aggregate types, domain event dispatch, the Mediator logging pipeline behavior). Kept in-repo rather than as a separate NuGet package since this template is meant to be a single, self-contained starting point - split it out if you end up sharing it across multiple solutions.

# When To Abstract

Clean Architecture should make change easier. It shouldn't make a simple database lookup pass through five layers of indirection. The template *supports* each of these building blocks, but a feature only uses one when it buys something:

| Building block | Add it when | Don't add it when |
|---|---|---|
| **Interface** | It sits on a layer boundary: UseCases or Core defines it and Infrastructure implements it (`ICurrentUser`, `IEmailSender`), or a real second implementation exists | It only wraps EF Core or another framework API and adds nothing to it, or it exists only so a class can be mocked |
| **Repository + specification** | The write path loads an aggregate, changes it through domain methods and saves it (`UserByIdSpecification`), or several handlers reuse the same lookup | It's a one-off read. Project straight to a DTO in a query service (`IListUsersQueryService`) instead |
| **Query service** | It's a read model, a projection, a join, pagination or hand-written SQL | The handler needs the aggregate's behavior, so use the repository |
| **Domain event** | Another part of the system reacts to something that happened in the domain, and the aggregate shouldn't know who that is | It's only "save a record". The `User` slice raises `UserRegisteredDomainEvent`, whose handler sends the confirmation email |
| **Mediator pipeline behavior** (`Web/Behaviors`) | It's a cross-cutting concern for every use case (`LoggingBehavior`), or for every message carrying a marker interface (`CachingBehavior` and `ICacheable`) | It's logic for one feature. Put that in its handler |

Every use case still goes through Mediator. That one seam stays uniform on purpose, so endpoints, jobs and tests all dispatch the same way.

# Supported vs Required

A template should *support* a capability without *requiring* every application to use it. This simple variant goes further and leaves out what most projects never need. The full template, [dotnet-angular-aspire-template](https://github.com/andregabrielmelo/dotnet-angular-aspire-template), has each of these built, tested and documented, ready to copy:

- background jobs (Hangfire) and job management
- a transactional outbox and inbox, for work that must reliably follow a database change
- an audit log
- output caching (this variant has HybridCache with Redis, ADR 017)
- file storage (S3, with Garage locally) and image processing
- ETags and optimistic concurrency
- URL-versioned endpoints
- Keycloak (OpenID Connect) with a backend for frontend and third-party sign-in
- log redaction and a global request timeout
- business metrics and end-to-end browser tests

A capability added to a project later should stay **opt-in**: registered through one `Add<Capability>()` extension, with the app building and its tests passing without it, and no feature referencing its types unless that feature uses it.

# Transactions And Side Effects

What happens, and in what order, when a use case changes data and something else should follow.

**When changes commit.**
- A use case changes entities through `IRepository<T>`, and each `AddAsync`/`UpdateAsync`/`DeleteAsync` calls `SaveChanges`. With no explicit transaction, **each `SaveChanges` is its own transaction**: it commits when it returns.
- Inside `Database.BeginTransactionAsync()`, nothing is visible to anyone else until `CommitAsync`. A rollback undoes every `SaveChanges` since the transaction began.

**Domain events are in-process and best-effort.**
- An entity registers one with `RegisterDomainEvent` (`User.MarkRegistered()` registers `UserRegisteredDomainEvent`). `EventDispatchInterceptor` publishes it through Mediator **right after `SaveChanges` succeeds**, in the same request. A use case can also dispatch right after saving (`RegisterUserHandler` does, because the event carries the id the insert assigned); it's just as best-effort.
- **A failing handler doesn't fail the request.** The change is already saved, so the error is logged and the request carries on. A 500 for a committed write would invite the client to repeat it. (`DomainEventDispatchTests`)
- **Inside an explicit transaction, handlers run before the commit,** so they also run when it later rolls back. (`DomainEventDispatchTests`)
- **Nothing retries them,** and a crash right after the commit loses them.

So a domain event handler that sends an email works, but the email can be lost, and it adds its latency to the request. That's acceptable for a confirmation email, which the user can ask for again (`POST /auth/resend-confirmation`). When it isn't, or for calls to other systems that must happen, use a transactional outbox: the full template's ADR 016 and its `Infrastructure/Outbox/` are the reference.

# The Frontend

Angular, kept deliberately unopinionated beyond the `auth`, `home`, `profile` and `users` features and a `core`/`shared` split. It holds no credential: the session is an HttpOnly cookie the browser sends by itself, so a reload or a new tab stays signed in, and HttpClient echoes the antiforgery cookie as a header. The Angular dev server proxies `/api` to the Web API over HTTPS, so the app has one origin and no CORS. See [ADR 015]({{< relref "architecture-decisions/adr-015-cookie-sessions-aspnetcore-identity" >}}) and the [single-origin notes]({{< relref "notes/cors-and-proxy" >}}).

# Authentication

The API manages accounts with ASP.NET Core Identity (passwords, lockout, roles) and signs users in with Identity's own application cookie: HttpOnly, `SameSite=Strict`, always `Secure`, lasting 14 days (sliding, Identity's default), with no tokens in the browser. Signed-in requests other than GET also need an antiforgery token. Logout deletes the cookie. See [ADR 015]({{< relref "architecture-decisions/adr-015-cookie-sessions-aspnetcore-identity" >}}) and [ADR 013]({{< relref "architecture-decisions/adr-013-user-management-aspnetcore-identity" >}}). Roles and permissions are smart enums; the account's Identity roles grant the permissions, which Identity's claims factory writes into the session; see [ADR 011]({{< relref "architecture-decisions/adr-011-role-based-permissions" >}}). Email confirmation is required before sign-in; password reset and change come from Identity too, and a password change ends every other session within 30 minutes (the security stamp). Login, registration and the account email endpoints are rate limited per client address, on top of a global limiter; see [ADR 014]({{< relref "architecture-decisions/adr-014-rate-limiting-and-security-headers" >}}). There's no 2FA or external sign-in; Identity supports both, and Keycloak brings them in the full template.

# Orchestration

[.NET Aspire](https://learn.microsoft.com/dotnet/aspire/) wires up Postgres (containerized, with a persistent data volume), Mailpit (catching development emails), the Web API and the Angular app (via Aspire's JavaScript app hosting, `npm ci` + `npm start`) as one thing you run and observe together in local development, with service discovery and the Aspire dashboard for logs/traces. It's not used for anything in production - deploy the API and the built Angular app however you'd normally deploy them, behind one origin (see [Production deployment]({{< relref "best-practices#production-deployment" >}}), and `PublishAsDockerFile()` on the frontend resource in `AppHost.cs` for one option). Mailpit runs only in the `Development` environment; elsewhere the AppHost expects an SMTP server (the `mailserver-hostname` and `mailserver-port` parameters). Every request is HTTPS, development included ([ADR 016]({{< relref "architecture-decisions/adr-016-https-in-development" >}})).
