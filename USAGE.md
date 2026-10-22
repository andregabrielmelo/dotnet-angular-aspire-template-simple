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

Copy the shape of the `User` feature (a full vertical slice) rather than starting from nothing:

1. **Core** - add an aggregate under `Aggregates/<YourAggregate>Aggregate/`, plus any value objects and specifications it needs.
2. **UseCases** - add commands/queries and their handlers under `<YourAggregate>s/<Action>/`.
3. **Infrastructure** - add an `IEntityTypeConfiguration<T>` under `Data/Configurations/`, a query service if you need read-optimized queries, then generate a migration (see below).
4. **Web** - add one FastEndpoints class per endpoint under `Features/<YourAggregate>Features/`, each with its own FluentValidation validator.
5. **Tests** - a unit test class per handler in `AppTemplate.UnitTests`, and functional tests for the new endpoints in `AppTemplate.FunctionalTests`.

Delete the `User` feature once you no longer need it as a reference.

## Common commands

| Task | Command |
|---|---|
| Run everything | `dotnet run --project backend/src/AppTemplate.AppHost` |
| Backend tests | `dotnet test backend/AppTemplate.slnx` |
| Backend format check | `dotnet csharpier check .` (from `backend/`) |
| New migration | see [backend/README.md](backend/README.md) |
| Frontend dev server | `npm start` (from `frontend/`) |
| Frontend tests | `npm run test` (from `frontend/`) |
| Frontend lint | `npm run lint` (from `frontend/`) |

Formatting and linting run automatically: a git pre-commit hook reformats staged C# files with csharpier (see `.husky/`), and CI enforces both csharpier and prettier/eslint on every push.

## Where things live

- `backend/` - .NET solution (Clean Architecture: Core/UseCases/Infrastructure/Web + tests)
- `frontend/` - Angular app
- `docs/` - Hugo docs site (architecture, ADRs, guides) - deployed to GitHub Pages
- `.github/workflows/` - CI (build/test/lint per stack, CodeQL, docs deploy) - see `.github/workflows/README.md`
- `scripts/` - repo tooling (currently just the rename script)

## Getting help with this template itself

Open an issue on the template repo, or see [.github/CONTRIBUTING.md](.github/CONTRIBUTING.md) if you want to fix something in the template.
