---
title: "FAQ"
weight: 90
---

# Frequently asked questions

Short answers, with links to where each choice is argued in full. Modeled on [NimblePros/eShopOnWeb's FAQ](https://github.com/NimblePros/eShopOnWeb/blob/main/docs/faq.md).

{{% details "Why a session cookie for the app, but JWTs as well?" %}}
The Angular app uses Identity's HttpOnly session cookie: no script on the page can read it, so a cross-site scripting bug can't steal the session, and there are no tokens to refresh or revoke ([ADR 015]({{< relref "architecture-decisions/adr-015-cookie-sessions-aspnetcore-identity" >}})). Clients that aren't a browser on the app's origin, such as scripts, mobile apps or other services, can't hold that cookie, so `POST /auth/token` gives them a short-lived bearer token with the same claims ([ADR 018]({{< relref "architecture-decisions/adr-018-bearer-tokens-for-non-browser-clients" >}})). The app itself never uses one.
{{% /details %}}

{{% details "Why Mediator and not MediatR?" %}}
[Mediator](https://github.com/martinothamar/Mediator) is source-generated: handlers are wired at compile time, with no reflection at startup, and it's MIT licensed, while MediatR became commercial. Their APIs are close, so MediatR examples translate directly. An architecture test (`NoLayer_UsesMediatR`) keeps a second dispatcher out ([ADR 003]({{< relref "architecture-decisions/adr-003-fastendpoints-mediator" >}})).
{{% /details %}}

{{% details "Why is there no AutoMapper?" %}}
Mapping is written by hand, in the endpoint or the use case: a few lines that the compiler checks, that a reader can follow, and that break visibly when a property is renamed. A mapper hides those decisions in configuration and fails at runtime instead. AutoMapper has also become commercial.
{{% /details %}}

{{% details "Why do list queries use FromSqlRaw?" %}}
A list is a read model, not an aggregate: it goes through a query service (`ListUsersQueryService`) instead of the repository. It starts from `FromSqlRaw` over every mapped column, with inputs only as parameters, and does the ordering, paging and projection in LINQ on top. The SQL stays visible and tunable, and EF still composes the rest safely. See [Best practices]({{< relref "best-practices" >}}) and the [cookbook's list recipe]({{< relref "guides/cookbook#filter-or-sort-a-list" >}}).
{{% /details %}}

{{% details "Do I need Redis?" %}}
No. HybridCache keeps an in-process cache on its own; Redis is its shared second level, which the AppHost starts in development. Without Redis, reads still work. Writes that invalidate the cache answer 500 while Redis is configured but down ([ADR 017]({{< relref "architecture-decisions/adr-017-hybrid-cache" >}})). With several API instances, configure it, or an instance can serve a stale entry until it expires locally.
{{% /details %}}

{{% details "Why do some tests need Docker?" %}}
The integration and functional tests run against a real Postgres in a container (Testcontainers), because EF Core's in-memory provider hides exactly the behavior that breaks in production: unique indexes, cascades, raw SQL and concurrency ([ADR 006]({{< relref "architecture-decisions/adr-006-testing-strategy" >}})). Skip them with `--filter "Category!=RequiresDocker"`, or `dotnet scripts/BuildTestFormat.cs --no-docker`.
{{% /details %}}
