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

- [.NET 10 SDK](https://dotnet.microsoft.com/download), 10.0.114 or a later 10.0 feature band (`global.json` pins it)
- [Node.js](https://nodejs.org/) (LTS) and npm
- [Docker](https://www.docker.com/) or Podman (Aspire runs Postgres and Mailpit in containers)
- A trusted ASP.NET Core development certificate: every request is HTTPS, development included ([ADR 016]({{< relref "architecture-decisions/adr-016-https-in-development" >}})). Run once, then restart the browser:

  ```bash
  dotnet dev-certs https --trust
  ```

  - **In WSL,** the command also trusts the certificate in Windows, so Edge and Firefox there accept it.
  - **For `curl` in WSL or Linux,** add `export SSL_CERT_DIR=$HOME/.aspnet/dev-certs/trust:/usr/lib/ssl/certs` to your profile.
  - **`aspire certs trust`** does the same with the Aspire CLI.
- The [Aspire CLI/workload](https://learn.microsoft.com/dotnet/aspire/fundamentals/setup-tooling), or just run the AppHost project from your IDE

### Or: the dev container

Instead of installing those, open the repository in the dev container (`.devcontainer/`): in VS Code, **Reopen in Container** (Dev Containers extension), or create a GitHub Codespace. It has the .NET 10 SDK, Node 22 (as CI uses), a Docker engine for Aspire's containers and the tests, PowerShell for Playwright, and the C#, CSharpier, Angular, ESLint and Prettier extensions. On creation, `post-create.sh`:

- creates and trusts a development certificate inside the container, and sets `SSL_CERT_DIR` so Node and .NET trust it
- restores the .NET tools and packages, and runs `npm ci`

Then run the AppHost as in step 4; VS Code forwards the dashboard (port 17243) and the app's ports to your machine.

**The browser on your machine** doesn't trust the container's certificate, so it warns on the app's `https://localhost` pages. To trust it, import `.devcontainer/dev-cert.crt` (its public part, exported on creation and ignored by git) into your machine's trusted root certificates once:

- **Windows:** `certutil -user -addstore Root .devcontainer\dev-cert.crt`, then restart the browser.
- **macOS:** `security add-trusted-cert -r trustRoot -k ~/Library/Keychains/login.keychain-db .devcontainer/dev-cert.crt`
- **Linux:** add it to the browser's certificate store (Chromium and Firefox: Settings, Certificates, Authorities, Import).

A new container creates a new certificate, so do this again after rebuilding it. Remove the old one when you do.

The browser tests (`AppTemplate.AspireTests`) also need Chromium's system libraries: run `pwsh backend/tests/AppTemplate.AspireTests/bin/Debug/net10.0/playwright.ps1 install --with-deps chromium` once, after the first build.

## 4. Run it

```bash
cd backend
dotnet run --project src/YourProjectName.AppHost
```

The Aspire dashboard opens and starts Postgres (in a container, with a persistent data volume), Mailpit (catching development emails, such as the confirmation and reset links), the Web API, and the Angular frontend (via `npm ci` + `npm start`; its dev server proxies `/api` to the API - see the [CORS/proxy notes]({{< relref "notes/cors-and-proxy" >}})). Open the app from the `angular` resource's `https://` URL in the dashboard, then register and open the confirmation link from Mailpit (its web UI is the `mailpit` resource's URL), or sign in as the development admin, `admin@example.com` / `Admin123!`. On first run in Development, EF Core migrations are applied and the database is seeded automatically (see [`DatabaseConfigurations`](https://github.com/andregabrielmelo/dotnet-angular-aspire-template-simple/blob/main/backend/src/AppTemplate.Web/Configurations/DatabaseConfigurations.cs)).

Before pushing, run what CI runs in one go, from the repository root:

```bash
dotnet scripts/BuildTestFormat.cs
```

It restores, checks formatting, builds, runs every test project, checks that the generated API files are current, and runs the frontend's format check, lint, tests and build, then prints a pass/fail summary. Add `--no-docker` to skip the tests that need Docker, or `--backend`/`--frontend` for one side.

## 5. Add your own feature

The `User` aggregate/feature (`Core/Aggregates/UserAggregate`, `UseCases/Users`, `Web/Endpoints/Users`) is the reference example - a full vertical slice from domain entity to HTTP endpoint. Copy its shape for your first real feature, then delete `User` once you no longer need it as a reference.

[Adding a feature, step by step]({{< relref "guides/adding-a-feature" >}}) walks through exactly that, with a complete `Project` example. Start with [How the project fits together]({{< relref "guides/project-map" >}}) for a map of the code.

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

- [Guides]({{< relref "guides" >}}) - how the project fits together, adding a feature, common tasks
- [Goals & Design Decisions]({{< relref "design-decisions" >}}) - why the stack looks the way it does
- [Architecture Decisions]({{< relref "architecture-decisions" >}}) - specific decisions and their trade-offs
- [Best Practices]({{< relref "best-practices" >}}) - explicit conventions to follow as the project grows
- [API Reference]({{< relref "api-reference" >}}) - how the OpenAPI docs work
