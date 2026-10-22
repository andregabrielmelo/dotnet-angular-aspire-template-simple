---
title: "Common tasks"
weight: 30
---

# Common tasks

Short recipes for things that come up after the first feature. Each one lists the files to touch and a file in the repository that already does it. For a whole new feature, follow [Adding a feature, step by step]({{< relref "adding-a-feature" >}}) instead.

## Add a permission and protect an endpoint

1. Add the constant to `UseCases/Authorization/Permission.cs` and include it in `All`. Every name in `All` becomes an ASP.NET Core policy automatically.
2. Add it as a client role on `apptemplate-api` in `AppHost/Realms/apptemplate-realm.json`, and to the composites of the realm roles that should have it (such as `admin`). Then delete the Keycloak data volume, or add the role in the admin console, because the realm file is imported only once.
3. Protect the endpoint in the way that fits:
   - **Who you are** decides it: `Policies(Permission.X)` in `Configure()`. See `Web/Features/UserFeatures/ListEndpoint.cs`.
   - **What you touch** decides it ("the owner, or anyone with X"): check in the handler with `ICurrentUser`, and return `Result.Forbidden()`. See `UseCases/Users/Update/UpdateUserHandler.cs`.
4. Add it to `frontend/src/app/core/auth/permissions.ts`, so the UI can hide what the user can't do.
5. In functional tests, grant it with `factory.CreateAuthenticatedClient(sub, Permission.X)`, and test both with and without it.

## Make an endpoint public

1. Call `AllowAnonymous()` in `Configure()`. Without it, the authorization fallback policy requires a signed-in user.
2. Add the route to `ExpectedAnonymousRoutes` in `FunctionalTests/Authorization/EndpointAuthorizationTests.cs`. The test fails until you do, so making something public is always a reviewed change.
3. Give it a stricter rate limit if it does anything expensive or sends email ([below](#limit-or-time-out-one-endpoint)).
4. For a browser on a signed-out page to reach it, the backend for frontend needs a route that requires no session. In `BackendForFrontend/Configurations/ReverseProxyConfigurations.cs`, add a route before the `api` one:
   ```csharp
   private const string AnonymousApiRouteId = "api-anonymous";

   // in the routes list:
   new()
   {
       RouteId = AnonymousApiRouteId,
       ClusterId = "web",
       Order = -1,
       Match = new RouteMatch { Path = "/api/newsletter", Methods = [HttpMethods.Post] },
   },
   ```
   Then let the path transform apply to it as well: change `if (context.Route.RouteId is not ApiRouteId)` to `is not (ApiRouteId or AnonymousApiRouteId)`. The second check in that transform keeps the access token off the anonymous route. The `X-CSRF` header is still required. Add a test in `BackendForFrontend.Tests/ProxyRoutingTests.cs`.

## Filter or sort a list

1. Add the parameters to the request class, with `[BindFrom("snake_case_name")]` (see `ListUsersRequest` in `Web/Features/UserFeatures/ListEndpoint.cs`), and validate them, for example against a fixed set of sort keys.
2. Pass them through the query record to the query-service interface.
3. **Filters** go in the query service's `FromSqlRaw` SQL as `WHERE` conditions, with each value as a `{n}` placeholder, which EF sends as a parameter:
   ```csharp
   _db.Projects.FromSqlRaw(
       "SELECT id, owner_external_id, name, created_at_utc FROM projects WHERE owner_external_id = {0} AND name ILIKE {1}",
       ownerExternalId,
       $"%{nameContains}%"
   )
   ```
   For an optional filter, choose between two constant SQL strings (with and without the condition) in code. Never assemble the SQL from request values.
4. **Sorting** is LINQ on top, never SQL text built from input. Map each allowed sort key to a fixed `OrderBy` (for example a `switch` over `"name"`/`"created"`), and end with a unique column (`.ThenBy(p => p.Id)`) so pages don't repeat or skip rows. Never paste a sort key or any other request value into the SQL string.
5. Add an index for a filter used on every call (`builder.HasIndex(...)`, then a migration).
6. Test it in functional tests: the filter, each sort order, and paging with the filter applied.

## Send an email

`IEmailSender` (`Core/Interfaces/IEmailSender.cs`) sends through MailKit to the `Mailserver` host, which is Mailpit when you run the AppHost. Open the `mailpit` resource's http endpoint in the Aspire dashboard to read what was sent.

1. Inject `IEmailSender` into the handler (or domain event handler) that decides to send, and call `SendEmailAsync(to, from, subject, body, cancellationToken)`.
2. Keep addresses and subjects in an options class rather than in code ([Add a setting](#add-a-setting)).
3. In functional tests the factory replaces the sender with `FakeEmailSender`. Assert on `factory.EmailSender.Sent`, as the recipe below does.
4. Don't log the email address or body. They're personal data and are redacted, but it's better not to log them at all.

Sending inside the request adds the SMTP round trip to its time, and a failure isn't retried. For mail that must arrive, use a transactional outbox (see the full template).

## React to a domain event

A domain event lets one part of the system react to something without the aggregate knowing who listens. `User.Create` raises `UserProvisioned` when someone signs in for the first time. This handler sends them a welcome email. It was compiled and tested against this template:

```csharp
// UseCases/Users/Provisioned/SendWelcomeEmailWhenUserProvisioned.cs
using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.Aggregates.UserAggregate.Events;
using AppTemplate.Core.Aggregates.UserAggregate.Specifications;

namespace AppTemplate.UseCases.Users.Provisioned;

/// <summary>
/// Runs in-process right after the new user is saved. Best-effort: if sending fails, the
/// failure is logged and the sign-in still succeeds, and nothing retries the email.
/// </summary>
public sealed class SendWelcomeEmailWhenUserProvisioned(
    IRepository<User> _repository,
    IEmailSender _emailSender
) : IDomainEventHandler<UserProvisioned>
{
    public async ValueTask Handle(UserProvisioned notification, CancellationToken cancellationToken)
    {
        var user = await _repository.FirstOrDefaultAsync(
            new UserByExternalIdSpecification(notification.ExternalId),
            cancellationToken
        );
        if (user is null)
            return;

        await _emailSender.SendEmailAsync(
            user.Email.Value,
            "no-reply@apptemplate.local",
            "Welcome!",
            $"Hi {user.Name.Value}, your account is ready.",
            cancellationToken
        );
    }
}
```

```csharp
// FunctionalTests/Events/WelcomeEmailTests.cs
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class WelcomeEmailTests(AppTemplateWebApplicationFactory factory)
    : IClassFixture<AppTemplateWebApplicationFactory>
{
    [Fact]
    public async Task FirstSignIn_SendsAWelcomeEmail()
    {
        var subject = $"sub-{Guid.NewGuid():N}";

        await factory.CreateAuthenticatedClient(subject).GetAsync("/users/me");

        Assert.Contains(factory.EmailSender.Sent, email => email.To == $"{subject}@example.com");
    }
}
```

**How this works:**
- **Registration is automatic.** Mediator's source generator finds every handler.
- **To raise your own event,** create a class deriving from `DomainEventBase` in the aggregate's `Events/` folder (see `UserProvisioned.cs`), and call `RegisterDomainEvent(new YourEvent(...))` in the aggregate method where it happens.
- **When the handler runs:** after `SaveChanges` succeeds, in the same request. If it throws, the error is logged and the request still succeeds. Inside an explicit transaction, it runs before the commit. See [Transactions And Side Effects]({{< relref "design-decisions#transactions-and-side-effects" >}}).
- **Keep handlers short.** They add to the request's time.

## Add a setting

Settings are classes bound from configuration and validated at startup, never read ad hoc with `configuration["Key"]`. Follow `MailserverConfiguration` (`Infrastructure/Email/`):

1. **The class:** a `SectionName` constant, properties, and safe defaults in the class (not in `appsettings.json`).
   ```csharp
   public class WelcomeEmailOptions
   {
       public const string SectionName = "WelcomeEmail";

       public string From { get; set; } = "no-reply@apptemplate.local";
       public string Subject { get; set; } = "Welcome!";
   }
   ```
2. **Register and validate it** in `Web/Configurations/OptionConfigurations.cs`:
   ```csharp
   services
       .AddOptions<WelcomeEmailOptions>()
       .Bind(configuration.GetSection(WelcomeEmailOptions.SectionName))
       .Validate(
           o => !string.IsNullOrWhiteSpace(o.From) && !string.IsNullOrWhiteSpace(o.Subject),
           "WelcomeEmail needs a From address and a Subject."
       )
       .ValidateOnStart();
   ```
3. **Use it** by injecting `IOptions<WelcomeEmailOptions>` and reading `.Value`.
4. **Test it:** add the type to `WebOptions()` in `FunctionalTests/Configuration/OptionsConfigurationTests.cs`, which checks it's registered and validated.
5. **Set values per environment** in `appsettings.{Environment}.json`, environment variables (`WelcomeEmail__From`) or, for local secrets, user secrets. A secret never goes in a committed file.

A required value with no sensible default fails startup when missing: validate that it's present. An optional feature is off when its setting is empty, but once turned on, every setting is validated (Options, in [Best Practices]({{< relref "best-practices#options" >}})).

## Talk to an external service

Anything outside the process, such as an HTTP API, a payment provider or a message queue, sits behind a port:

1. **The interface** goes in `Core/Interfaces/`, in your domain's terms rather than the vendor's. Its doc comment names the implementation and who uses it (see `IEmailSender.cs`).
2. **The implementation** goes in `Infrastructure/<Concern>/` (for example `Infrastructure/Payments/`), with its options class next to it.
3. **Register it** in `Infrastructure/InfrastructureServiceExtensions.cs`. For HTTP, use `services.AddHttpClient<IThing, ThingClient>(...)`; ServiceDefaults adds retries and timeouts to every `HttpClient`.
4. **Pick a timeout:** if a request waits for it, check whether the 30-second request timeout is enough ([below](#limit-or-time-out-one-endpoint)).
5. **Tests:**
   - Handlers depend on the interface, so unit tests substitute it.
   - In functional tests, replace it with a fake in `AppTemplateWebApplicationFactory`, as `FakeEmailSender` replaces `IEmailSender`.
   - Test the real client against a stub HTTP handler, not the live service.
6. **Health:** if the app can't serve anything without it, add a health check tagged `ready`; otherwise, make the calling code fail gracefully ([Health checks]({{< relref "best-practices#health-checks" >}})).

## Limit or time out one endpoint

**A stricter rate limit**, for a public form or an expensive call:
1. Add a policy in `Web/Configurations/RateLimitingConfigurations.cs`, next to the global limiter:
   ```csharp
   options.AddPolicy(
       "newsletter",
       context => FixedWindow(ClientPartition.Of(context), 5, TimeSpan.FromMinutes(1))
   );
   ```
2. Apply it: `Options(x => x.RequireRateLimiting("newsletter"))` in the endpoint's `Configure()`. Keep the name in a constant.

Rejections are 429 problem details with `Retry-After`. Don't use FastEndpoints' `Throttle`.

**A longer timeout**, for an endpoint that waits on a slow service:
1. Add a policy in `Web/Configurations/RequestTimeoutConfigurations.cs`: `options.AddPolicy("external-call", new RequestTimeoutPolicy { Timeout = ..., TimeoutStatusCode = 504 })`.
2. Apply it with `Options(x => x.WithRequestTimeout("external-call"))`.

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
4. Run the tests. `MigrationDriftTests` fails if the model and the migrations disagree.

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
| Business metrics | `UseCases/Telemetry/ApplicationMetrics.cs` |
| Browser end-to-end tests | `tests/AppTemplate.EndToEndTests` (Playwright) |
