# Backend

.NET 10 solution following Clean Architecture (`Core` / `UseCases` / `Infrastructure` / `Web`), orchestrated locally via Aspire.

```bash
dotnet run --project src/AppTemplate.AppHost   # starts Postgres, the API, and the frontend together
dotnet build AppTemplate.slnx
dotnet test AppTemplate.slnx                   # xUnit: AppTemplate.UnitTests + AppTemplate.FunctionalTests
dotnet csharpier check .                       # formatting, enforced in CI
```

New migration:

```bash
dotnet ef migrations add YourMigrationName \
  --project src/AppTemplate.Infrastructure \
  --startup-project src/AppTemplate.Web \
  -o Data/Migrations
```

See the [docs site](https://andregabrielmelo.github.io/dotnet-angular-aspire-template-simple/) for the architecture, design decisions, and ADRs - start with [Getting Started](https://andregabrielmelo.github.io/dotnet-angular-aspire-template-simple/getting-started/).
