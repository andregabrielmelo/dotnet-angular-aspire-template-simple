# Usage

Quick reference for working in a project started from this template. For the full picture (architecture, design decisions, ADRs), see the [docs site](https://andregabrielmelo.github.io/dotnet-angular-aspire-template-simple/).

## First time setup

```bash
scripts/rename-template.sh YourProjectName   # AppTemplate -> YourProjectName, everywhere
```

Then see [backend/README.md](backend/README.md) and [frontend/README.md](frontend/README.md) for prerequisites and day-to-day commands. Or run everything at once:

```bash
cd backend
dotnet run --project src/YourProjectName.AppHost
```

## Adding a feature

Follow [Adding a feature, step by step](docs/content/guides/adding-a-feature.md): it builds a complete `Project` feature with the code for every step, from the domain model to the Angular page and the tests. In short, copying the shape of the `User` feature:

1. **Core** - add an aggregate under `Aggregates/<YourAggregate>Aggregate/`, plus any value objects and specifications it needs.
2. **UseCases** - add commands/queries and their handlers under `<YourAggregate>s/<Action>/`.
3. **Infrastructure** - add an `IEntityTypeConfiguration<T>` under `Data/Configurations/`, a query service if you need read-optimized queries, then generate a migration (see below).
4. **Web** - add one FastEndpoints class per endpoint under `Features/<YourAggregate>Features/`, each with its own FluentValidation validator.
5. **Tests** - a unit test class per handler in `AppTemplate.UnitTests`, and functional tests for the new endpoints in `AppTemplate.FunctionalTests`.

Delete the `User` feature once you no longer need it as a reference. For smaller tasks (a permission, a public endpoint, sending email, a new setting) see [Common tasks](docs/content/guides/cookbook.md), and for orientation, [How the project fits together](docs/content/guides/project-map.md).

## Common commands

| Task | Command |
|---|---|
| Run everything | `dotnet run --project backend/src/AppTemplate.AppHost` |
| Backend tests | `dotnet test backend/AppTemplate.slnx` (functional tests need Docker or Podman) |
| Backend format check | `dotnet csharpier check .` (from `backend/`) |
| New migration | see [backend/README.md](backend/README.md) |
| Frontend dev server | `npm start` (from `frontend/`) |
| Frontend tests | `npm run test` (from `frontend/`) |
| Frontend lint | `npm run lint` (from `frontend/`) |

There is no pre-commit hook: run `dotnet csharpier format .` (from `backend/`) and `npm run format` (from `frontend/`) before committing. CI enforces csharpier, prettier and eslint on every push.

## Where things live

- `backend/` - .NET solution (Clean Architecture: Core/UseCases/Infrastructure/Web + tests)
- `frontend/` - Angular app
- `docs/` - Hugo docs site (architecture, ADRs, guides) - deployed to GitHub Pages
- `.github/workflows/` - CI (build/test/lint per stack, PR conventions, secret scan, docs deploy) - see `.github/workflows/README.md`
- `scripts/` - repo tooling (the rename script and the OpenAPI export)

## Need more than this?

This is the simple variant. Background jobs, a transactional outbox, auditing, caching, file storage, ETags and API versioning are built, tested and documented in the full template, [dotnet-angular-aspire-template](https://github.com/andregabrielmelo/dotnet-angular-aspire-template). Copy what you need from there, together with its ADR.

## Getting help with this template itself

Open an issue on the template repo, or see [.github/CONTRIBUTING.md](.github/CONTRIBUTING.md) if you want to fix something in the template.
