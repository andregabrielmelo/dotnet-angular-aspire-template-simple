---
title: "Getting Started"
weight: 10
---

# Getting Started

## 1. Create your repo from this template

Click **Use this template** on GitHub (or `gh repo create your-project --template andregabrielmelo/dotnet-angular-aspire-template-simple --clone --public`), then clone it locally.

## 2. Rename `AppTemplate`

Every project, namespace, folder, the Postgres database name, and the frontend's session-storage key are currently named `AppTemplate` / `apptemplate`. Rename them in one pass:

```bash
scripts/rename-template.sh YourProjectName
```

This is a plain find-and-replace across the repo (not a `dotnet new` template engine) - review the diff afterward and commit it as your first real commit.

## 3. Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) (LTS) and npm
- [Docker](https://www.docker.com/) or Podman (Aspire runs Postgres, Keycloak and Mailpit in containers)
- The [Aspire CLI/workload](https://learn.microsoft.com/dotnet/aspire/fundamentals/setup-tooling), or just run the AppHost project from your IDE

## 4. Run it

```bash
cd backend
dotnet run --project src/YourProjectName.AppHost
```

The Aspire dashboard opens and starts Postgres (in a container, with a persistent data volume), Keycloak (with the `apptemplate` realm imported), Mailpit (catching development emails), the Web API, the backend for frontend, and the Angular frontend (via `npm ci` + `npm start`, served through the backend for frontend - see the [CORS/proxy notes]({{< relref "notes/cors-and-proxy" >}})). Open the app at `https://localhost:7100`; the development realm has an `admin`/`admin` account. On first run in Development, EF Core migrations are applied and the database is seeded automatically (see [`DatabaseConfigurations`](https://github.com/andregabrielmelo/dotnet-angular-aspire-template-simple/blob/main/backend/src/AppTemplate.Web/Configurations/DatabaseConfigurations.cs)).

## 5. Add your own feature

The `User` aggregate/feature (`Core/Aggregates/UserAggregate`, `UseCases/Users`, `Web/Features/UserFeatures`) is the reference example - a full vertical slice from domain entity to HTTP endpoint. Copy its shape for your first real feature, then delete `User` once you no longer need it as a reference.

## 6. Migrations

From the `backend` directory:

```bash
dotnet ef migrations add YourMigrationName \
  --project src/YourProjectName.Infrastructure \
  --startup-project src/YourProjectName.Web \
  -o Data/Migrations
```

Migrations are applied automatically on startup in Development (see `DatabaseConfigurations.StartDatabase`), or set `Database:ApplyMigrationsOnStartup` to apply them elsewhere.

## Further reading

- [Goals & Design Decisions]({{< relref "design-decisions" >}}) - why the stack looks the way it does
- [Architecture Decisions]({{< relref "architecture-decisions" >}}) - specific decisions and their trade-offs
- [Best Practices]({{< relref "best-practices" >}}) - explicit conventions to follow as the project grows
- [API Reference]({{< relref "api-reference" >}}) - how the OpenAPI docs work
