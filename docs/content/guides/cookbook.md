---
title: "Common tasks"
weight: 30
---

# Common tasks

Short recipes for things that come up after the first feature. Each one lists the files to touch and a file in the repository that already does it. For a whole new feature, follow [Adding a feature, step by step]({{< relref "adding-a-feature" >}}) instead.

## Add a permission and protect an endpoint

1. Add the constant to `UseCases/Authorization/Permission.cs` and include it in `All`.
2. Grant it to the roles that should have it in `UseCases/Authorization/RolePermissions.cs` (`Admin` gets `Permission.All` already). Users get it in their next access token (`UserPrivilegesExtensions.AddUser` writes it as a `permissions` claim), at the latest on the next refresh.
3. Protect the endpoint in the way that fits:
   - **Who you are** decides it: `Permissions(Permission.X)` in `Configure()`. See `Web/Endpoints/Users/ListEndpoint.cs`.
   - **What you touch** decides it ("the owner, or anyone with X"): check in the handler with `ICurrentUser`, and return `Result.Forbidden()`. See `UseCases/Users/Update/UpdateUserHandler.cs`.
4. Add it to `frontend/src/app/core/auth/permissions.ts`, so the UI can hide what the user can't do.
5. In functional tests, sign in with a role that has it and one that doesn't (`factory.CreateSignedInSessionAsync(Roles.Admin)` and `factory.CreateSignedInSessionAsync()`), and test both. Add a case to `RolePermissionsFor` (unit tests) for the roles that get it.

## Make an endpoint public

1. Call `AllowAnonymous()` in `Configure()`. Without it, FastEndpoints requires a signed-in user.
2. Add the route to `ExpectedAnonymousRoutes` in `FunctionalTests/EndpointAuthorization.cs`. The test fails until you do, so making something public is always a reviewed change.
3. If it does anything expensive or sends email, add a rate limit first ([below](#limit-one-endpoint)); the template has none.
4. The SPA reaches it like any other `api/...` URL; `authInterceptor` adds an access token only if there is one, and a public endpoint simply ignores it.

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

## React to a domain event

A domain event lets one part of the system react to something without the aggregate knowing who listens. The template has one, end to end: registering raises `UserRegisteredDomainEvent`, and its handler emails the confirmation link.

```csharp
// Core/Aggregates/UserAggregate/Events/UserRegisteredDomainEvent.cs
public sealed record UserRegisteredDomainEvent(UserId UserId) : DomainEventBase;

// UseCases/Users/Register/SendConfirmationOnUserRegistered.cs
public sealed class SendConfirmationOnUserRegistered(IIdentityService _identity)
    : IDomainEventHandler<UserRegisteredDomainEvent>
{
    public async ValueTask Handle(
        UserRegisteredDomainEvent domainEvent,
        CancellationToken cancellationToken
    ) => await _identity.SendEmailConfirmationAsync(domainEvent.UserId, cancellationToken);
}
```

`AuthRegisterPost` (functional tests) checks that registering sends it; `SendConfirmationOnUserRegisteredHandle` (unit tests) covers the handler on its own. To add a second reaction, such as a welcome email with your own text, add another `IDomainEventHandler<UserRegisteredDomainEvent>`. The handler lives in UseCases because it needs a UseCases port; a handler that only needs Core's ports (`IEmailSender`) can live next to the aggregate.

**How this works:**
- **Registration is automatic.** Mediator's source generator finds every handler.
- **To raise your own event,** create a record deriving from `DomainEventBase` in the aggregate's `Events/` folder, carrying ids and values rather than the aggregate itself, and call `RegisterDomainEvent(new YourEvent(...))` in the aggregate method where it happens. An event that needs the database-assigned id is published by the use case once the entity is saved, as `RegisterUserHandler` does with `IMediator.Publish`.
- **When the handler runs:** after `SaveChanges` succeeds, in the same request (`EventDispatchInterceptor`); if it throws, the error is logged and the request still succeeds. Or when the use case publishes it itself right after saving (`RegisterUserHandler`); then a throwing handler fails the request, after the user was saved. Inside an explicit transaction, it runs before the commit. See [Transactions And Side Effects]({{< relref "design-decisions#transactions-and-side-effects" >}}).
- **Keep handlers short.** They add to the request's time.

## Add a role

1. Add the constant to `UseCases/Authorization/Roles.cs` and to `All`.
2. Seed it in `Infrastructure/Data/Configurations/ApplicationRoleConfiguration.cs` with the next id, its upper-case `NormalizedName` and a fixed `ConcurrencyStamp`, then add a migration. Postgres doesn't advance the identity column for seeded ids, so roles are never created at runtime.
3. Say what it grants in `UseCases/Authorization/RolePermissions.cs`.
4. To give someone the role, add a method to `IIdentityService` that calls `UserManager.AddToRoleAsync` in `IdentityService`, and a use case and endpoint over it (protected by a permission). It reaches the user in their next access token.
5. Test it: a case in `RolePermissionsFor`, `factory.CreateSignedInSessionAsync(Roles.YourRole)` in functional tests, and the migrations' seeded roles in `IdentityServiceCreateAccount.TheMigrationsSeedTheRoles`.

## Send an account email

Account emails (confirmation, password reset) carry Identity codes, so they are built in Infrastructure ([ADR 013]({{< relref "architecture-decisions/adr-013-user-management-aspnetcore-identity" >}})):
1. Generate the code with `UserManager` in `IdentityService` (for example `GenerateChangeEmailTokenAsync`), Base64Url-encode it, and build the link with `Link(...)`, which starts from `AccountLinks:ClientUrl`. Add the page's path to `AccountLinkSettings`.
2. Send it from `AccountEmailSender` (Identity's `IEmailSender<ApplicationUser>`), or a method of your own next to it.
3. Add the page that opens the link in `frontend/src/app/features/auth/`, reading the query parameters as component inputs, as `confirm-email-page` does, and a route in `auth.routes.ts`.
4. Expose a port method and a use case; the endpoint that takes the code is anonymous, so add it to `EndpointAuthorization`. Never log the link or the code.
5. Test the code round trip in `IntegrationTests/Identity/` (`RecordingEmailSender.LastLinkTo`), and the endpoint with `factory.EmailSender.LastLinkQueryTo(email)`.

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

The template has no rate limiting. To limit a public form, an expensive call, or sign-in:
1. Register ASP.NET Core's rate limiter in Web, with a named policy partitioned by client address (or by the validated `sub` for signed-in callers):
   ```csharp
   services.AddRateLimiter(options =>
   {
       options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
       options.AddPolicy(
           "newsletter",
           context => RateLimitPartition.GetFixedWindowLimiter(
               context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
               _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) }
           )
       );
   });
   ```
   and `app.UseRateLimiter()` after `UseAuthentication()` in `MiddlewareConfigurations`.
2. Apply it: `Options(x => x.RequireRateLimiting("newsletter"))` in the endpoint's `Configure()`. Keep the name in a constant.

Behind a proxy, the client address is the proxy's unless you add the forwarded headers middleware with that proxy as a known one. Don't use FastEndpoints' `Throttle`: it trusts the raw `X-Forwarded-For` header. The full template has a complete setup to copy.

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
