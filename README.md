# dotnet-angular-aspire-template-simple

![Backend Build](https://github.com/andregabrielmelo/dotnet-angular-aspire-template-simple/actions/workflows/backend-build.yml/badge.svg)
![Frontend Build](https://github.com/andregabrielmelo/dotnet-angular-aspire-template-simple/actions/workflows/frontend-build.yml/badge.svg)

A GitHub template repository for starting new full-stack projects with:

- **.NET 10** backend using **Clean Architecture** (Core / UseCases / Infrastructure / Web)
- **FastEndpoints** + **Mediator** for the API (REPR pattern + CQRS)
- **EF Core** + **PostgreSQL** for persistence
- **Angular** frontend
- **.NET Aspire** orchestrating all of the above for local development

It's not a sample app - it's a skeleton with one real feature (`User`) wired end-to-end so you can see where things belong. Replace it with your own domain once you understand the shape.

This is the simple variant, with what most projects need: Keycloak sign-in through a backend for frontend, permissions, email, logging, health checks and tests. For background jobs, a transactional outbox, auditing, caching, file storage, ETags or API versioning, see the full template, [dotnet-angular-aspire-template](https://github.com/andregabrielmelo/dotnet-angular-aspire-template), and copy what you need from there.

**📖 Full docs: [andregabrielmelo.github.io/dotnet-angular-aspire-template-simple](https://andregabrielmelo.github.io/dotnet-angular-aspire-template-simple/)**

## Quickstart

1. Click **Use this template** above (or `gh repo create your-project --template andregabrielmelo/dotnet-angular-aspire-template-simple --clone --public`).
2. Rename `AppTemplate` to your project's name:
   ```bash
   scripts/rename-template.sh YourProjectName
   ```
3. Run everything via Aspire:
   ```bash
   cd backend
   dotnet run --project src/YourProjectName.AppHost
   ```

See [Getting Started](https://andregabrielmelo.github.io/dotnet-angular-aspire-template-simple/getting-started/) for prerequisites and details.

## Docs

- [Getting Started](https://andregabrielmelo.github.io/dotnet-angular-aspire-template-simple/getting-started/)
- [Goals & Design Decisions](https://andregabrielmelo.github.io/dotnet-angular-aspire-template-simple/design-decisions/)
- [Architecture Decisions (ADRs)](https://andregabrielmelo.github.io/dotnet-angular-aspire-template-simple/architecture-decisions/)
- [Further reading](https://andregabrielmelo.github.io/dotnet-angular-aspire-template-simple/further-reading/)

## Inspiration

This template builds on two well-known Clean Architecture templates, and borrows their structure where it fits:

- [ardalis/CleanArchitecture](https://github.com/ardalis/CleanArchitecture) - the Core / UseCases / Infrastructure / Web layout, ports in Core, query services and the REPR endpoints.
- [jasontaylordev/CleanArchitecture](https://github.com/jasontaylordev/CleanArchitecture) - application pipeline behaviors (logging) and domain enums.

When a structural question comes up, [Further reading](https://andregabrielmelo.github.io/dotnet-angular-aspire-template-simple/further-reading/) lists the blogs and articles the design leans on.

## License

[MIT](LICENSE)
