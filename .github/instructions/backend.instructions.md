---
applyTo: "backend/**"
---

# Backend instructions

These extend [`AGENTS.md`](../../AGENTS.md); read it first. This file covers only the details of writing backend code.

## Endpoint shape

Model every endpoint on `backend/src/AppTemplate.Web/Features/UserFeatures/GetByIdEndpoint.cs`:

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
        Policies(Permission.UsersRead);           // or AllowAnonymous(), always explicit
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

- **Return types:** an endpoint returns `Results<Success, ProblemHttpResult>`, where the success type is `Ok<T>`, `Created<T>`, `NoContent` or `Accepted`. Every failure is a problem details response ([ADR 009](../../docs/content/architecture-decisions/adr-009-problem-details-error-contract.md)), so declare the possible ones with `ProducesProblem(status)` in `Description(...)`.
- **Validators** check request shape: required fields, lengths, formats and cross-field rules. Business invariants belong in Core value objects and aggregates, not in validators.
- **Mapping:** `Ardalis.Result` maps to HTTP only through `Web/Extensions/ResultExtensions.cs`. If a new mapping is needed, add it there.
- **Rate limiting:** a global limiter covers every endpoint. Sensitive or expensive endpoints add a named policy in `RateLimitingConfigurations` and apply it with `Options(x => x.RequireRateLimiting(...))`. Limits key on the validated `sub`, or on the client address (trusted only from `ForwardedHeaders:KnownProxies`). Never use FastEndpoints' `Throttle(...)`.

## Use case shape

```
UseCases/<Feature>/<UseCase>/
  <UseCase>Command.cs   // or <UseCase>Query.cs: record : ICommand<Result<T>> / IQuery<Result<T>>
  <UseCase>Handler.cs   // ICommandHandler / IQueryHandler, one Handle method
```

- **Dependencies:** handlers take `IRepository<T>`, query-service interfaces, ports from `Core/Interfaces` (such as `IEmailSender`), and `ICurrentUser`. They never take infrastructure types.
- **Unit tests:** put them in `tests/AppTemplate.UnitTests/UseCases/<Feature>/<UseCase>HandlerTests.cs`.

## Adding a persistent type

1. Define the aggregate in `Core/Aggregates/<Name>Aggregate/`, with a Vogen ID that has a `Validate` method.
2. Register the converter in `Infrastructure/Data/Configurations/VogenEfCoreConverters.cs`.
3. Add the `IEntityTypeConfiguration` with `HasValueGenerator<VogenIdValueGenerator<...>>()`, and a `DbSet` in `ApplicationDatabaseContext`.
4. Add the migration (see CLAUDE.md), review it, then run `dotnet csharpier format .`.
5. Add a functional test that saves and reads the entity through the API, against real Postgres.

## Functional tests

- **Setup:**
  - Use `IClassFixture<AppTemplateWebApplicationFactory>`.
  - Add `[Trait(TestCategories.Name, TestCategories.RequiresDocker)]`.
  - Create clients with `factory.CreateAuthenticatedClient(sub, Permission.X)`.
- **What to test:** for each endpoint, cover the success path, invalid input (400), no permission (403), a missing resource (404) and any conflict (409). Errors are problem details, so assert the status code and, where it matters, `errors`/`detail` (see `ProblemDetailsContractTests`).
- **Replacing services:** use `factory.WithWebHostBuilder(b => b.ConfigureTestServices(...))`, as `DomainEventDispatchTests` and `RequestTimeoutTests` do.
