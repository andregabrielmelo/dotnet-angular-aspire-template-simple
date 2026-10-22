---
title: "ADR 001: Clean Architecture layering"
weight: 10
---

# ADR 001: Clean Architecture layering (Core / UseCases / Infrastructure / Web)

## Status
Accepted

## Context
A new full-stack project needs a backend structure that keeps the domain model free of framework and persistence concerns, so business logic can be tested and evolved without dragging EF Core or ASP.NET Core along with it. A single-project structure is faster to start with but makes that separation a matter of discipline rather than something the compiler enforces.

## Decision
Use a four-project Clean Architecture layout, based on [Ardalis' Clean Architecture template](https://github.com/ardalis/CleanArchitecture):

- **Core** - domain model (aggregates, value objects, specifications, domain events) and the ports to external resources (`Core/Interfaces`, such as the email sender), depends on almost nothing.
- **UseCases** - CQRS commands/queries, plus the list query-service interfaces (they return use-case DTOs) and `ICurrentUser` (request context, implemented in Web); depends on Core only.
- **Infrastructure** - EF Core, email, and other outward-facing implementations: of the ports in Core, and of the query services in UseCases.
- **Web** - FastEndpoints HTTP API, the composition root.

A shared `SharedKernel` folder holds base types (`EntityBase`, domain event dispatch) used across layers.

## Consequences
- Project references enforce the dependency rule at compile time - Core cannot accidentally take a dependency on EF Core.
- Adding a feature touches multiple projects (an aggregate in Core, a handler in UseCases, a repository/query service in Infrastructure, an endpoint in Web), which is more ceremony than a single-project vertical-slice approach for small features.
- Project-to-project references mean full-solution builds are slower than a single project, though incremental builds are unaffected.
- If a project's domain stays small, the multi-project overhead may not be worth it - see [Ardalis' Minimal Clean Architecture template](https://github.com/ardalis/CleanArchitecture/blob/main/docs/content/minimal-clean-architecture.md) for a single-project alternative and a migration path between the two.
