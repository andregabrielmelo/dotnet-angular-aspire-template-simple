---
applyTo: "backend/**"
---

# Backend instructions

These extend [`AGENTS.md`](../../AGENTS.md); read it first. This file covers only the details of writing backend code. For a complete worked example of every backend layer, see [Adding a feature, step by step](../../docs/content/guides/adding-a-feature.md) (steps 2 to 7 and 9).

## Endpoint shape

Model every endpoint on `backend/src/AppTemplate.Web/Endpoints/Users/GetByIdEndpoint.cs` (namespace `AppTemplate.Web.Endpoints.Users`):

```csharp
public sealed class GetUserByIdRequest            // dedicated request DTO, no Data Annotations
{
    public int Id { get; set; }
}

public class GetByIdEndpoint(IMediator mediator)
    : Endpoint<GetUserByIdRequest, Results<Ok<UserRecord>, ProblemHttpResult>, GetUserByIdMapper>
{
    public override void Configure()
    {
        Get("/users/{id}");
        Permissions(Permission.UsersRead);        // or AllowAnonymous(), always explicit
        Summary(s => { /* summary, examples, one line per status code */ });
        Tags("Users");
        Description(b => b.Produces<UserRecord>(200, "application/json").ProducesProblem(404));
    }

    public override async Task<Results<Ok<UserRecord>, ProblemHttpResult>> ExecuteAsync(
        GetUserByIdRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetUserQuery(UserId.From(request.Id)), cancellationToken);
        return result.ToOkResult(Map.FromEntity);   // ResultExtensions, never a hand-written switch
    }
}

public sealed class GetByIdUserValidator : Validator<GetUserByIdRequest> { /* request shape only */ }

public sealed class GetUserByIdMapper : Mapper<GetUserByIdRequest, UserRecord, UserDto> { /* DTO mapping */ }
```

- **Return types:** an endpoint returns `Results<Success, ProblemHttpResult>`, where the success type is `Ok<T>`, `Created<T>`, `NoContent` or `Accepted`. Every use case failure is a problem details response ([ADR 009](../../docs/content/architecture-decisions/adr-009-problem-details-error-contract.md)), so declare the possible ones with `ProducesProblem(status)` in `Description(...)`.
- **Validators** check request shape: required fields, lengths, formats and cross-field rules. Business invariants belong in Core value objects and aggregates, not in validators.
- **Mapping:** `Ardalis.Result` maps to HTTP only through `Web/Extensions/ResultExtensions.cs`. If a new mapping is needed, add it there.
- **Authentication:** see [ADR 015](../../docs/content/architecture-decisions/adr-015-cookie-sessions-aspnetcore-identity.md) (the session cookie and antiforgery) and [ADR 013](../../docs/content/architecture-decisions/adr-013-user-management-aspnetcore-identity.md) (accounts). Endpoints read the caller through `ICurrentUser` (in use cases); they never read the cookie or its claims themselves. Account operations are use cases over `IIdentityService`; add a method there (implemented in `Infrastructure/Identity/IdentityService` with `UserManager`) rather than using Identity from Web or UseCases. Identity's failures become `Result.Error` (`IdentityResultExtensions.ToApplicationResult`). Account emails carry codes: build them in `IdentityService`, send them through `AccountEmailSender`, and never log the links or codes.
- **Rate limits:** see [ADR 014](../../docs/content/architecture-decisions/adr-014-rate-limiting-and-security-headers.md). Every anonymous, email-sending or expensive endpoint calls `Options(builder => builder.RequireRateLimiting(RateLimitingConfigurations.AuthenticationPolicy))` (or a named policy of its own in `RateLimitingConfigurations`) and documents the 429. Never use FastEndpoints' `Throttle(...)`, and never partition by a header the client sends.

## Use case shape

```
UseCases/<Feature>/Commands/<UseCase>/   // or Queries/<UseCase>/ for a query
  <UseCase>Command.cs   // or <UseCase>Query.cs: record : ICommand<Result<T>> / IQuery<Result<T>>
  <UseCase>Handler.cs   // ICommandHandler / IQueryHandler, one Handle method
```
UseCases/<Feature>/Handlers/   // domain event handlers

- **Dependencies:** handlers take `IRepository<T>`, query-service interfaces, ports from `Core/Interfaces` (such as `IEmailSender`), and `ICurrentUser`. They never take infrastructure types.
- **List query services** (`Infrastructure/Data/Queries/`) read with `FromSqlRaw`: every mapped column in snake_case, `WHERE` with `{n}` parameters only, and `OrderBy`/`Skip`/`Take`/`Select`/`CountAsync` in LINQ on top. See `ListUsersQueryService` and step 3 of [Adding a feature](../../docs/content/guides/adding-a-feature.md#3-infrastructure-the-database).
- **Unit tests:** put them in `tests/AppTemplate.UnitTests/UseCases/<Feature>/<UseCase>HandlerHandle.cs` (class `<UseCase>HandlerHandle`, one method per behavior).

## Adding a persistent type

1. Define the aggregate in `Core/Aggregates/<Name>Aggregate/`, with a Vogen ID that has a `Validate` method.
2. Register the converter in `Infrastructure/Data/Configurations/VogenEfCoreConverters.cs`.
3. Add the `IEntityTypeConfiguration`, with the ID as `ValueGeneratedOnAdd().HasVogenConversion()` (and `deserializationStrictness: DeserializationStrictness.AllowAnything` on the Vogen ID, since EF's temporary keys are negative), and a `DbSet` in `ApplicationDatabaseContext`.
4. Add the migration (see CLAUDE.md), review it, then run `dotnet csharpier format .`.
5. Add integration tests for its repository behavior and constraints (`tests/AppTemplate.IntegrationTests/Data/`), and functional tests that save and read it through the API.

## Functional tests

- **Setup:**
  - One class per route and verb in `tests/AppTemplate.FunctionalTests/ApiEndpoints/`, named like `UserGetById` or `AuthLoginPost`.
  - Use `IClassFixture<CustomWebApplicationFactory<Program>>`, and add `[Trait(TestCategories.Name, TestCategories.RequiresDocker)]`.
  - Sign in for real: `var (user, session) = await factory.CreateSignedInSessionAsync(Role.Admin);`, then call the API with `session.Client`.
- **What to test:** for each endpoint, cover the success path, invalid input (400), no permission (403), a missing resource (404) and any conflict (409). Assert status codes with `Ardalis.HttpClientTestExtensions` (`GetAndEnsureNotFoundAsync`, ...), and, where it matters, `errors`/`detail`, with Shouldly.
- **Replacing services:** subclass `CustomWebApplicationFactory<Program>` and override `ConfigureWebHost`, as `OptionsValidationAtStartup` does; a test that moves the clock (`factory.Time`) gets its own factory class.
