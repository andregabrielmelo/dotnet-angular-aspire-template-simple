---
title: "Further reading"
weight: 95
---

# Further reading

Where to look when the ADRs don't settle a design question. These are the sources this template's structure leans on; when a decision here follows one of them, the ADR says so.

## Templates this project builds on

- [ardalis/CleanArchitecture](https://github.com/ardalis/CleanArchitecture): the Core / UseCases / Infrastructure / Web layout ([ADR 001]({{< relref "architecture-decisions/adr-001-clean-architecture-layering" >}})), ports in `Core/Interfaces`, query services in UseCases, `Ardalis.Result`, `Ardalis.Specification` and REPR endpoints with FastEndpoints. Its [MailserverConfiguration](https://github.com/ardalis/CleanArchitecture/blob/main/src/Clean.Architecture.Infrastructure/Email/MailserverConfiguration.cs) is the shape of this template's typed settings.
- [jasontaylordev/CleanArchitecture](https://github.com/jasontaylordev/CleanArchitecture): application pipeline behaviors (its [PerformanceBehaviour](https://github.com/jasontaylordev/CleanArchitecture/blob/main/src/Application/Common/Behaviours/PerformanceBehaviour.cs) is the model for `UseCases/Behaviors`).

## Articles behind specific decisions

By Milan Jovanović:

- [Clean Architecture](https://milanjovanovic.tech/articles/clean-architecture): the layering as a whole.
- [The Infrastructure layer in Clean Architecture](https://milanjovanovic.tech/blog/infrastructure-layer-clean-architecture): one folder per external concern in Infrastructure (Data, Email).
- The full template, [dotnet-angular-aspire-template](https://github.com/andregabrielmelo/dotnet-angular-aspire-template), lists the articles behind its outbox, inbox, background jobs, audit log and caching, for when a project adds them.

## Blogs worth checking when in doubt

- [Milan Jovanović](https://milanjovanovic.tech/blog): practical .NET architecture, EF Core and messaging patterns.
- [Ardalis (Steve Smith)](https://ardalis.com/blog/): Clean Architecture, DDD, specifications and the Result pattern.
- [Jason Taylor](https://jasontaylor.dev/): Clean Architecture with ASP.NET Core and the template's own reasoning.
- [Martin Fowler](https://martinfowler.com/): the patterns underneath (enterprise application architecture, refactoring, testing, distributed systems).

None of these is a rulebook. When one conflicts with an ADR, the ADR wins, and changing it means a new or amended ADR.
