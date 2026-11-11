---
title: "Common tasks"
weight: 30
---

# Common tasks

Short recipes for things that come up after the first feature. Each one lists the files to touch and a file in the repository that already does it. For a whole new feature, follow [Adding a feature, step by step]({{< relref "adding-a-feature" >}}) instead.

## Add a permission and protect an endpoint

1. Add an instance to the `Permission` smart enum (`UseCases/Authorization/Permission.cs`): `public static readonly Permission ProjectsRead = new(nameof(ProjectsRead), "projects:read");`. `Permission.List` picks it up.
2. Grant it to the roles that should have it in `UseCases/Authorization/Role.cs` (`Admin` gets `[.. Permission.List]` already). Users get it at their next sign-in or security stamp check, within 30 minutes (`ApplicationClaimsPrincipalFactory` writes it as a `permissions` claim).
3. Protect the endpoint in the way that fits:
   - **Who you are** decides it: `Permissions(Permission.X)` in `Configure()`. See `Web/Endpoints/Users/ListEndpoint.cs`.
   - **What you touch** decides it ("the owner, or anyone with X"): check in the handler with `ICurrentUser`, and return `Result.Forbidden()`. See `UseCases/Users/Commands/Update/UpdateUserHandler.cs`.
4. Add it to `frontend/src/app/core/auth/permissions.ts`, so the UI can hide what the user can't do.
5. In functional tests, sign in with a role that has it and one that doesn't (`factory.CreateSignedInSessionAsync(Role.Admin)` and `factory.CreateSignedInSessionAsync()`), and test both. Add a case to `RolePermissionsFor` (unit tests) for the roles that get it. In text, write `Permission.X.Value`: its `ToString()` is the C# name.

## Make an endpoint public

1. Call `AllowAnonymous()` in `Configure()`. Without it, FastEndpoints requires a signed-in user.
2. Add the route to `ExpectedAnonymousRoutes` in `FunctionalTests/EndpointAuthorization.cs`. The test fails until you do, so making something public is always a reviewed change.
3. Give it a rate limit policy ([below](#limit-one-endpoint)), especially if it does anything expensive or sends email.
4. The SPA reaches it like any other `api/...` URL; the browser sends the session cookie only if there is one, and a public endpoint simply ignores it.

## Filter or sort a list

1. Add the parameters to the request class, with `[BindFrom("snake_case_name")]` (see `ListUsersRequest` in `Web/Endpoints/Users/ListEndpoint.cs`), and validate them, for example against a fixed set of sort keys.
2. Pass them through the query record to the query-service interface.
3. **Filters** go in the query service's `FromSqlRaw` SQL as `WHERE` conditions, with each value as a `{n}` placeholder, which EF sends as a parameter:
   ```csharp
   _db.Projects.FromSqlRaw(
       "SELECT id, owner_id, name, created_at_utc FROM projects WHERE owner_id = {0} AND name ILIKE {1}",
       ownerId.Value,
       $"%{nameContains}%"
   )
   ```
   For an optional filter, choose between two constant SQL strings (with and without the condition) in code. Never assemble the SQL from request values.
4. **Sorting** is LINQ on top, never SQL text built from input. Map each allowed sort key to a fixed `OrderBy` (for example a `switch` over `"name"`/`"created"`), and end with a unique column (`.ThenBy(p => p.Id)`) so pages don't repeat or skip rows. Never paste a sort key or any other request value into the SQL string.
5. Add an index for a filter used on every call (`builder.HasIndex(...)`, then a migration).
6. Test it in functional tests: the filter, each sort order, and paging with the filter applied.

## Send an email

`IEmailSender` (`Core/Interfaces/IEmailSender.cs`) sends through MailKit to the `Mailserver` host, which is Mailpit when you run the AppHost. Open the `mailpit` resource's http endpoint in the Aspire dashboard to read what was sent.

1. Inject `IEmailSender` into the handler (or domain event handler) that decides to send, and call `SendAsync(to, subject, body, cancellationToken)`. The sender is `Mailserver:FromAddress`.
2. Keep addresses and subjects in an options class rather than in code ([Add a setting](#add-a-setting)).
3. In functional tests the factory replaces the sender with `FakeEmailSender`. Assert on `factory.EmailSender.Sent`, as the recipe below does.
4. Don't log the email address or body. They're personal data, and nothing redacts logs.

Sending inside the request adds the SMTP round trip to its time, and a failure isn't retried. For mail that must arrive, use a transactional outbox (see the full template).

## Send an account email

Emails that carry an Identity code (confirmation, password reset, and anything like them you add, such as an email change) go through ASP.NET Core Identity, not straight through `IEmailSender` ([ADR 013]({{< relref "architecture-decisions/adr-013-user-management-aspnetcore-identity" >}})):
1. **Port:** add a method to `UseCases/Interfaces/IIdentityService.cs`, taking and returning only the application's types (`UserId`, `EmailAddress`, `Result`).
2. **Code and link:** in `Infrastructure/Identity/IdentityService.cs`, generate the code with `UserManager` (`Generate...TokenAsync`), Base64Url-encode it (`Encode`), and build the link with `Link(path, query...)`, which starts from `AccountLinks:ClientUrl`. Put the path in `AccountLinkSettings`, next to `ConfirmEmailPath` and `ResetPasswordPath`.
3. **The email:** send it through `AccountEmailSender` (Identity's `IEmailSender<ApplicationUser>`), adding a method for a new kind of email.
4. **The endpoints:** a use case and an anonymous endpoint that takes the code back, with the `authentication` rate limit policy ([Limit one endpoint](#limit-one-endpoint)). The endpoint that sends answers the same whether or not the address has an account (202), and the one that takes the code answers a wrong or expired one with a 400 on `code` (`InvalidCodeResult`).
5. **The page:** a route in `frontend/src/app/features/auth/auth.routes.ts` at the link's path, reading its query parameters as inputs, and a call in `account.service.ts`.
6. **Tests:** `IdentityServiceEmailFlows` (integration, with the fixture's recording sender) and a functional test per endpoint that follows the link (`factory.EmailSender.LastLinkQueryTo(email)`). Add the anonymous route to `EndpointAuthorization`.

Never log the link, the code or the address. The codes are protected with Data Protection keys kept in Postgres, so a link works on every instance and after a restart.

## React to a domain event

A domain event lets one part of the system react to something without the aggregate knowing who listens. The template has one, end to end: registering raises `UserRegisteredDomainEvent`, and its handler emails the confirmation link.

```csharp
// Core/Aggregates/UserAggregate/Events/UserRegisteredDomainEvent.cs
public sealed record UserRegisteredDomainEvent(UserId UserId, UserName Name, EmailAddress Email)
    : DomainEventBase;

// UseCases/Users/Handlers/SendConfirmationOnUserRegistered.cs
public sealed class SendConfirmationOnUserRegistered(IIdentityService _identity)
    : IDomainEventHandler<UserRegisteredDomainEvent>
{
    public async ValueTask Handle(
        UserRegisteredDomainEvent domainEvent,
        CancellationToken cancellationToken
    ) => await _identity.SendEmailConfirmationAsync(domainEvent.UserId, cancellationToken);
}
```

`AuthRegisterPost` (functional tests) checks that registering sends it; `SendConfirmationOnUserRegisteredHandle` (unit tests) covers the handler on its own. It lives in UseCases because it needs `IIdentityService`; a handler that only needs a Core port (such as `IEmailSender`) can live next to the event in Core. To add a second reaction, add another `IDomainEventHandler<UserRegisteredDomainEvent>`. A handler must be `public`: Mediator's source generator registers it from the Web project.

**How this works:**
- **Registration is automatic.** Mediator's source generator finds every handler.
- **To raise your own event,** create a record deriving from `DomainEventBase` in the aggregate's `Events/` folder, carrying ids and values rather than the aggregate itself, and call `RegisterDomainEvent(new YourEvent(...))` in the aggregate method where it happens. An event that needs the database-assigned id is published by the use case once the entity is saved, as `RegisterUserHandler` does with `IMediator.Publish`.
- **When the handler runs:** after `SaveChanges` succeeds, in the same request (`EventDispatchInterceptor`); if it throws, the error is logged and the request still succeeds. Or when the use case publishes it itself right after saving (`RegisterUserHandler`); then a throwing handler fails the request, after the user was saved. Inside an explicit transaction, it runs before the commit. See [Transactions And Side Effects]({{< relref "design-decisions#transactions-and-side-effects" >}}).
- **Keep handlers short.** They add to the request's time.

## Add a role

1. Add an instance to the `Role` smart enum (`UseCases/Authorization/Role.cs`) with the next `Value` and the permissions it grants: `public static readonly Role Support = new(nameof(Support), 3, [Permission.UsersRead]);`.
2. Give it a fixed concurrency stamp in `Infrastructure/Data/Configurations/IdentityRoleConfiguration.cs` (it seeds `Role.List`), then add a migration for the new row. Postgres doesn't advance the identity column for seeded ids, so roles are never created at runtime.
3. To give someone the role, add a method to `IIdentityService` that calls `UserManager.AddToRoleAsync(account, role.Name)` in `IdentityService`, and a use case and endpoint over it (protected by a permission). It reaches the user's sessions at their next security stamp check, within 30 minutes.
4. Test it: a case in `RolePermissionsFor`, `factory.CreateSignedInSessionAsync(Role.Support)` in functional tests; `IdentityServiceCreateUser.TheMigrationsSeedTheRoles` checks the seeded rows against `Role.List`.

## Add a setting

Settings are classes bound from configuration and validated at startup, never read ad hoc with `configuration["Key"]`. Follow `MailserverConfiguration` (`Infrastructure/Email/`):

1. **The class:** a `SectionName` constant, properties, and safe defaults in the class (not in `appsettings.json`).
   ```csharp
   public class WelcomeEmailOptions
   {
       public const string SectionName = "WelcomeEmail";

       public string Subject { get; set; } = "Welcome!";
       public string SupportAddress { get; set; } = "support@apptemplate.local";
   }
   ```
2. **Register and validate it** in `Web/Configurations/OptionConfigurations.cs`:
   ```csharp
   services
       .AddOptions<WelcomeEmailOptions>()
       .Bind(configuration.GetSection(WelcomeEmailOptions.SectionName))
       .Validate(
           o => !string.IsNullOrWhiteSpace(o.Subject) && EmailAddress.IsValid(o.SupportAddress),
           "WelcomeEmail needs a Subject and a valid SupportAddress."
       )
       .ValidateOnStart();
   ```
3. **Use it** by injecting `IOptions<WelcomeEmailOptions>` and reading `.Value`.
4. **Test it:** add the type to `WebOptions()` in `FunctionalTests/OptionsRegistration.cs`, which checks it's registered and validated.
5. **Set values per environment** in `appsettings.{Environment}.json`, environment variables (`WelcomeEmail__Subject`) or, for local secrets, user secrets. A secret never goes in a committed file.

A required value with no sensible default fails startup when missing: validate that it's present. An optional feature is off when its setting is empty, but once turned on, every setting is validated (Options, in [Best Practices]({{< relref "best-practices#options" >}})).

## Talk to an external service

Anything outside the process, such as an HTTP API, a payment provider or a message queue, sits behind a port:

1. **The interface** goes in `Core/Interfaces/`, in your domain's terms rather than the vendor's. Its doc comment names the implementation and who uses it (see `IEmailSender.cs`).
2. **The implementation** goes in `Infrastructure/<Concern>/` (for example `Infrastructure/Payments/`), with its options class next to it.
3. **Register it** in `Infrastructure/InfrastructureServiceExtensions.cs`. For HTTP, use `services.AddHttpClient<IThing, ThingClient>(...)`; ServiceDefaults adds retries and timeouts to every `HttpClient`. Pass the request's `CancellationToken` through, so the call stops when the client goes away.
4. **Tests:**
   - Handlers depend on the interface, so unit tests substitute it.
   - In functional tests, replace it with a fake in `CustomWebApplicationFactory`, as `FakeEmailSender` replaces `IEmailSender`.
   - Test the real client against a stub HTTP handler, not the live service.
5. **Health:** if the app can't serve anything without it, add a health check tagged `ready`; otherwise, make the calling code fail gracefully ([Health checks]({{< relref "best-practices#health-checks" >}})).

## Limit one endpoint

Every request already counts against the global limiter ([ADR 014]({{< relref "architecture-decisions/adr-014-rate-limiting-and-security-headers" >}})). To add a stricter limit to a public form, an expensive call, or anything that sends email:
1. **The usual case:** reuse the `authentication` policy (5 a minute per client address) in the endpoint's `Configure()`, as `ForgotPasswordEndpoint` does:
   ```csharp
   Options(builder => builder.RequireRateLimiting(RateLimitingConfigurations.AuthenticationPolicy));
   s.Responses[429] = "Too many attempts from this address"; // in Summary()
   ```
2. **A limit of its own:** add a constant and a policy next to `AuthenticationPolicy` in `Configurations/RateLimitingConfigurations.cs`, with its limit in `RateLimitSettings` (validated in `OptionConfigurations`):
   ```csharp
   public const string NewsletterPolicy = "newsletter";

   options.AddPolicy(
       NewsletterPolicy,
       context => FixedWindow($"newsletter:{ClientAddress(context)}", settings.NewsletterPermitLimit)
   );
   ```
   Partition by `ClientAddress(context)` or the session's `user_id`, never by a header the client sends.
3. **Test it** with a low-limit factory, as `Security/RateLimiting.cs` does (`LowRateLimitsFactory`); the shared factory raises the limits so other tests don't hit them.

The client address comes from `X-Forwarded-For` only when the peer is in `ForwardedHeaders:KnownProxies`. Don't use FastEndpoints' `Throttle`: it trusts the raw header.

## Change the database schema

1. Change the entity and its `IEntityTypeConfiguration` in `Infrastructure/Data/Configurations/`.
2. From `backend/`, create the migration and format it:
   ```bash
   dotnet ef migrations add DescribeTheChange --project src/AppTemplate.Infrastructure --startup-project src/AppTemplate.Web -o Data/Migrations
   dotnet csharpier format .
   ```
3. Read the generated migration:
   - **Adding a required column to a table with rows:** EF adds a `defaultValue` to fill the existing rows. Unless the model declares that default, drop it in the same migration, or inserts that forget the column silently get it.
   - **Renames:** EF may generate drop-and-add, which loses data. Change it to `RenameColumn`.
4. Run the tests. `MigrationDrift` (integration tests) fails if the model and the migrations disagree.

In Development, migrations apply on startup. In production, run a migration bundle as a deployment step ([PostgreSQL & migrations]({{< relref "best-practices#postgresql--migrations" >}})).

## Add a page only some users can open

1. Create the page in `features/<feature>/`, with its HTTP calls in the feature's service.
2. Add a route in `app.routes.ts` with both guards: `canActivate: [authGuard, permissionGuard(Permission.X)]`. See the `users` route.
3. Show the link only when it's allowed: in the component, `protected readonly canX = () => this.currentUser.hasPermission(Permission.X);`, then `@if (canX()) { ... }` in the template. See `features/home/home-page.ts`.
4. Handle a 403 from the API anyway. The guard is a convenience, and the API is what enforces it (see `features/users/users-page.ts`).

## Need more than this template has?

The full template, [dotnet-angular-aspire-template](https://github.com/andregabrielmelo/dotnet-angular-aspire-template), has these built, tested and documented. Copy the code, its tests and its ADR together:

| Need | Look for, in the full template |
|---|---|
| Recurring or background work | Hangfire, `Infrastructure/Jobs/`, ADRs 012 and 013 |
| Work that must follow a database change reliably | the transactional outbox and inbox, `Infrastructure/Outbox/`, `Infrastructure/Inbox/`, ADR 016 |
| A record of who changed what | `Infrastructure/Auditing/`, ADR 017 |
| Caching | HybridCache with Redis, and output caching, ADR 011 |
| File uploads | `IFileStorage` over S3 (Garage locally), image processing, ADR 018 |
| Lost-update protection | ETags and `If-Match`, ADR 019 |
| Versioned URLs (`/v1/...`) | ADR 015 |
| Keycloak sign-in through a backend for frontend, password reset, external sign-in | `AppTemplate.BackendForFrontend`, the Keycloak realm, ADR 007 |
| Log redaction, a global request timeout, API security headers | `ServiceDefaults/Logging/`, `RequestTimeoutConfigurations`, `SecurityHeadersConfigurations` |
| Business metrics | `UseCases/Telemetry/ApplicationMetrics.cs` |
| Browser end-to-end tests | `tests/AppTemplate.EndToEndTests` (Playwright) |
