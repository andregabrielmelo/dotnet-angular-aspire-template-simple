---
title: "ADR 017: HybridCache for user reads, with Redis as its distributed layer"
weight: 170
---

# ADR 017: HybridCache for user reads, with Redis as its distributed layer

## Status
Accepted.

## Context
Some reads happen far more often than the data changes. The SPA calls `GET /users/me` on every page load, and a profile page reads `GET /users/{id}`; each one went to Postgres, and `/users/me` to the Identity account as well. Almost every endpoint is authenticated and permission-gated, so a cache must never serve data to someone who isn't allowed to see it, and must never serve stale data after a write.

ASP.NET Core's [HybridCache](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid?view=aspnetcore-10.0) combines an in-process cache (L1) with an optional distributed one (L2, any `IDistributedCache`), protects against stampedes (concurrent misses for one key share one call), and invalidates by tag. [Milan Jovanović's introduction](https://milanjovanovic.tech/blog/hybrid-cache-in-aspnetcore-new-caching-library) uses Redis as L2. The full template ([dotnet-angular-aspire-template](https://github.com/andregabrielmelo/dotnet-angular-aspire-template), its ADR 011) already built this; this ADR keeps its HybridCache half and leaves out output caching, since no whole response here is the same for every caller and worth caching.

## Decision
- **Queries opt in with `ICacheable`; a Mediator behavior caches them.** As in [ardalis/CleanArchitecture's sample](https://github.com/ardalis/CleanArchitecture/blob/main/sample/src/NimblePros.SampleToDo.Web/CachingBehavior.cs), a query implements `ICacheable` (`UseCases/Caching`: a `CacheKey` and `CacheTags`), and `CachingBehavior` (`Web/Behaviors`, after `LoggingBehavior`) caches its response through `HybridCache.GetOrCreateAsync`. Handlers have no cache code. `GetUserQuery` (key `GetUserQuery-{id}`), `GetCurrentUserQuery` (`GetCurrentUserQuery-{id}`) and `ListUsersQuery` (`ListUsersQuery-{page}-{perPage}`, with the default page and size filled in) are cacheable. A key is the query's name (`nameof`) and everything its result depends on, so two queries never share one; the list is the same for everyone the endpoint lets in (`users:read`). Unlike the sample, which keeps every response in `IMemoryCache` for a fixed time:
  - **Only successful results are cached.** A failure (`NotFound` for an id that doesn't exist yet) is returned but never stored, so it always reaches the handler. The behavior throws it out of HybridCache's factory, which stores nothing and hands the same answer to every caller joined to that fetch.
  - **Writes invalidate by tag**, so an update shows at once instead of when the entry expires.
  - **HybridCache, not `IMemoryCache`**, so entries are shared through Redis and concurrent misses share one call.
- **HybridCache is used directly**, with no cache port of our own: it is already an abstraction (an abstract class, which FusionCache can also implement). UseCases references `Microsoft.Extensions.Caching.Hybrid` for it.
- **Whole results are cached.** HybridCache serializes `Result<UserDto>` and `Result<CurrentUserDto>` with System.Text.Json, in Redis and, for mutable types, in L1 too. `Ardalis.Result`'s `[JsonInclude]`, Vogen's converters and the value objects' public constructors make them round-trip; `CachedResultRoundTrip` (unit) checks it through a second cache instance.
- **Tags: per user, and one for the list.** Every entry about a user carries `CacheTags.User(id)` (`users:{id}`), and every list page `CacheTags.UserList` (`users:list`), since a page depends on every user. `UpdateUserHandler` and `DeleteUserHandler` call `HybridCache.RemoveByTagAsync([CacheTags.User(user.Id), CacheTags.UserList])` after saving, so a write drops that user's entries and the list pages, never other users' entries. `RegisterUserHandler` drops `CacheTags.UserList` last, after publishing the registration (a new user's own entries need nothing, since misses aren't cached).
- **Cache profiles, a smart enum.** A query picks how long its result lives by overriding `ICacheable.CacheProfile` (`UseCases/Caching/CacheProfile`): `None` (the default), `Short` or `Long`. The enum names the profiles, so a query can't name one that doesn't exist; the settings hold their lifetimes: `None` uses `Caching:Expiration` / `Caching:LocalExpiration`, and the others `Caching:Profiles:{Name}` (`Short` 1 minute / 15 seconds, `Long` 1 hour / 5 minutes by default). Startup validation requires every profile, each with `0 < LocalExpiration <= Expiration`. `CachingBehavior` passes the profile's lifetimes (`CachingSettings.EntryOptionsFor`) to HybridCache. This is the typed version of the ardalis sample's `CacheProfile` string, with `None` in place of its `null`.
- **Lifetimes are settings** (`Caching`, `Infrastructure/Caching/CachingSettings`, validated at startup): `Expiration` (L2, 10 minutes by default) and `LocalExpiration` (L1, 1 minute, at most `Expiration`). The short L1 bounds how long another instance can serve a copy after an invalidation.
- **Infrastructure sets it up** (`AddInfrastructureServices`), through Aspire's client integration, as it does Postgres. When Aspire provides the `cache` connection, Redis is L2; without it (tests, or the API run alone) only L1 is used, which is correct for one instance.
- **Redis is a persistent development container** (AppHost): `ContainerLifetime.Persistent` with a data volume, so entries outlive an AppHost run, and Redis Insight to browse the keys. Entries still expire by the `Caching` settings.
- **Redis is optional for reads, not for writes.**
  - HybridCache logs and skips a failing L2, falling back to L1 and the handler, so a read never fails because of Redis.
  - The client fails each command at once while it knows it's disconnected (`BacklogPolicy.FailFast`, passed to Aspire's `AddRedisDistributedCache`) instead of queueing it until its 5-second timeout, and reconnects in the background.
  - Aspire's own Redis health check is turned off, so Redis never fails `/health`. There is no Redis health check; a Redis outage shows in HybridCache's logged L2 failures.
  - Invalidation is not caught: with Redis down, `RemoveByTagAsync` throws, so an update or delete saves and then answers 500. This is a deliberate exception to "code using an optional dependency fails open" (AGENTS.md): the write never reports success while its invalidation can't reach the other instances.
  - The full template's `FailOpenDistributedCache` (a wrapper that skipped Redis while disconnected) is left out: HybridCache's own fallback and the fail-fast client cover reads, and its check of the connection state was only a snapshot anyway.

## Consequences
- **On a hit the handler doesn't run.** Mark a query `ICacheable` only when its result depends on nothing but its key, and its handler does no per-caller authorization: the endpoint's authorization still runs first (FastEndpoints authorizes before Mediator), but a `Forbidden` decided inside a handler would be skipped. Never cache what depends on the caller, such as permissions, under a key without the caller.
- A new cached query needs a key, a tag, and a `RemoveByTagAsync` in every write that changes its result. Forgetting the last step means stale data, so cover it with a functional test like `Caching/UserCaching`.
- While Redis is down, writes to cached data, registration included, answer 500 although they were saved, and a client may retry one that already happened. A command already in flight when Redis dies can still wait for its timeout.
- With several API instances, another instance's L1 may serve an invalidated entry for up to `LocalExpiration`, and while Redis is down its L1 isn't invalidated at all.
- `Caching/RedisOutage` stops a real Redis mid-test and checks that reads still answer quickly, and that an update is saved but answers 500.
- The account's email is cached with `/users/me`. Nothing changes it today; a future "change email" feature must remove the user's tag too.
