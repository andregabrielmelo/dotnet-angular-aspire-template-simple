---
title: "Best Practices"
weight: 25
---

# Best Practices

Explicit guidelines for projects built from this template, so every project that starts here ends up consistent. These are conventions to *follow*, not a restatement of the [design decisions]({{< relref "design-decisions" >}}) (why the stack looks the way it does) or the [ADRs]({{< relref "architecture-decisions" >}}) (specific past decisions) - though there's naturally some overlap.

## C# & .NET

- Follow [Microsoft's C# coding conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions) (PascalCase types/public members, `I`-prefixed interfaces, one statement per line). The compiler won't enforce this - `.editorconfig` and `dotnet csharpier` are what actually keep it consistent; don't fight them with inline overrides.
- **Never add `System.ComponentModel.DataAnnotations` attributes (`[Required]`, etc.) to FastEndpoints request DTOs.** FastEndpoints validates through FluentValidation `Validator<T>` classes, not Data Annotations - the attributes silently do nothing. This template shipped with exactly that mistake in every `User` endpoint until it was caught; if you ever see a Data Annotations attribute on a request class next to a `Validator<T>`, delete the attribute.
- Repository + Specification (`IRepository<T>`, `Ardalis.Specification`) is for the **write** path, where domain rules matter. Read-only queries don't need it - project straight to a DTO in a query service (see `ListUsersQueryService`'s raw SQL) instead of loading and mapping full entities. Specifications used purely for lookups (not followed by a mutation) should call `.AsNoTracking()` - see `UserByIdSpecification`/`UserByEmailSpecification`.
- Wrap a primitive in a [Vogen](https://github.com/SteveDunn/Vogen) value object when it has a real invariant to enforce or a real type-confusion risk (IDs, emails). Don't wrap everything - plain DTOs and read-only projections (`UserDto`, `UserRecord`) are supposed to stay plain.
- EF Core: avoid queries in loops (N+1) - eager-load or batch instead. Reach for compiled queries only once a specific query is a measured hot path, not by default.
- Inject `ILogger<T>`, never call the static `Serilog.Log` directly (it isn't configured). Use message templates (`"{UserId} created", id`), not string interpolation - it's what makes structured log fields searchable. Change log levels in `appsettings.json` (see Logging below).
- Tests follow Arrange-Act-Assert, one behavior per test. See `AppTemplate.UnitTests` for the current shape.
- Endpoints require a signed-in session (Identity's cookie) by default. Only add `AllowAnonymous()` deliberately. Protect administrative operations with a permission (`Permissions(Permission.X)`), and put rules that depend on the loaded resource (ownership) in the use case via `ICurrentUser` (see [ADR 011]({{< relref "architecture-decisions/adr-011-role-based-permissions" >}})). Read the caller through `ICurrentUser`, never from the cookie or its claims directly. The browser holds no credential the app can read: the session is an HttpOnly cookie, and signed-in requests other than GET also carry an antiforgery token (see [ADR 015]({{< relref "architecture-decisions/adr-015-cookie-sessions-aspnetcore-identity" >}})). XSS can still act as the user while the page is open, which is why the strict CSP below matters.

## Angular

- How the app reaches the API and shows its errors: [The frontend: configuration and API errors]({{< relref "guides/frontend" >}}).
- Feature-folder structure (`core/`, `shared/`, `features/<feature>/`) is already the convention here - keep new code inside it rather than growing `components/`/`services/`-by-type folders. See the [official style guide](https://angular.dev/style-guide).
- Hyphenate file names, one concept per file, co-locate a component's `.ts`/`.html`/`.css`/`.spec.ts`.
- Prefer `inject()` at field-initializer time over constructor-parameter injection for new code - it reads better and infers types more reliably.
- For state: local component state first; a signal exposed (read-only, via `computed()`) from a service when a few components need to share it; a route-scoped feature store only once that's not enough; NgRx SignalStore only once a feature genuinely needs a full store. Don't start with a global store.
- Prefer native `[class]`/`[style]` bindings over `NgClass`/`NgStyle`.
- Tests run on Vitest (already the default here) - keep specs next to the file they test, as `app.spec.ts` already does.
- **HTTP lives in feature services.** Each feature has a `<feature>.service.ts` that owns its API calls and a `<feature>.model.ts` for the response shapes (see `features/users/`). Components call the service and never inject `HttpClient` - ESLint fails the build if they do.
- **Guards are UX, not security.** `authGuard` and `permissionGuard` decide what to *show*; the API enforces every permission on its own. Never treat a hidden route as protected data.
- **Errors** ([ADR 009]({{< relref "architecture-decisions/adr-009-problem-details-error-contract" >}})): branch on `HttpErrorResponse.status` for the message, and for a 400 read `error.error.errors` - an object of camelCase field name to messages, matching the form control names - to show each message next to its field. Never render a raw error body.
- **Strict typing is on** (`strict` and `strictTemplates` in `tsconfig.json`). Don't loosen it per file, and avoid `any`; give API responses an interface in the feature's model file.
- **Accessibility:** semantic elements, a `<label>` for every control, keyboard operation, and error text linked to its field with `aria-describedby`. `npm run lint` runs Angular's template accessibility rules - fix what it reports rather than disabling the rule.

## Security headers

- **The API sets none of its own** ([ADR 014]({{< relref "architecture-decisions/adr-014-rate-limiting-and-security-headers" >}})). The host that serves the app and `/api` in production (a reverse proxy, CDN or static host) sends them; see Production deployment below.
- **The Angular dev server sends the SPA's CSP** in development (`angular.json`, `serve.options.headers`), so a violation shows up locally.
- **No `X-XSS-Protection`.** Browsers removed the filter it controlled, and enabling it could introduce leaks.
- **Keep the build CSP-compatible.** The SPA must work under a strict `Content-Security-Policy`: scripts only from the same origin, no inline `<script>`, no inline event handlers, no `eval` (styles may allow `'unsafe-inline'`, because Angular injects component styles at runtime). Angular's critical CSS inlining is off in `angular.json`, because it adds an `onload` handler such a CSP blocks, which would leave the page unstyled. If you add a third-party script, font or image host, add its origin to the matching CSP directive instead of loosening `script-src`.
- **No response compression.** Compressing HTTPS responses that mix secrets with attacker-influenced input enables [BREACH](https://www.breachattack.com/)-style attacks, and the reverse proxy or CDN in front of a real deployment compresses static assets better anyway. Don't add `UseResponseCompression()` to the API.

## Production deployment

The AppHost is for local development ([ADR 002]({{< relref "architecture-decisions/adr-002-aspire-orchestration" >}})). Wherever you deploy:

- **Serve the SPA and `/api` from one origin** (the default this template assumes): the host strips `/api` before forwarding to the API, as the dev server proxy does, so the session and antiforgery cookies (`SameSite=Strict`) stay first-party and no CORS is needed.
  - `app.example.com` with `api.example.com` (same site, different origins) also works: enable CORS on the API for exactly the app's origin with credentials, and send requests `withCredentials`. Angular's HttpClient then no longer adds the antiforgery header by itself (it only does for same-origin URLs), so add it in an interceptor.
  - A fully cross-site SPA and API needs a `SameSite=None; Secure` cookie and is not supported as is.
- **Security headers from the host,** for the SPA and `/api`: `Content-Security-Policy` (as above), `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`, `X-Frame-Options: DENY` (or `frame-ancestors 'none'`), and `Strict-Transport-Security` over HTTPS. The API sends none of its own.
- **List the proxies in front of the API** in `ForwardedHeaders__KnownProxies__0` (and `__1`, ...), as IP addresses; in JSON settings it's the array `ForwardedHeaders:KnownProxies`. Only then are `X-Forwarded-For` and `X-Forwarded-Proto` honored; without it, every client behind the proxy shares the proxy's rate limit partition.
- **Account links:** `AccountLinks__ClientUrl` is the public URL of the web app, where the confirmation and reset links lead. Startup fails without it.
- **Data Protection keys** (which protect the confirmation and reset codes) are in the `data_protection_keys` table, so every instance shares them. Anyone who can read that table can forge codes; add `ProtectKeysWith...` (a certificate or key vault) if the database isn't trusted enough.
- **Sign-out ends one browser's session.** A copied session cookie stays valid until it expires (14 days, sliding) or the account's security stamp changes (a password change or reset); see [ADR 015]({{< relref "architecture-decisions/adr-015-cookie-sessions-aspnetcore-identity" >}}). The Data Protection keys in Postgres also protect the cookie, so every instance accepts it.
- **Rate limits are per instance.** The limiter is in memory; with several instances, each counts separately, so use a distributed limiter or limit at the edge (WAF, CDN), which also stops attacks spread over many addresses.
- **Hide `/_test_url_cache_`.** FastEndpoints 8.3 maps it outside its pipeline, so it lists every route and endpoint type to anyone; block it at the host or move to FastEndpoints 8.4+ (ADR 012).

## Logging

- **Two providers, one set of events.** `LoggerConfigs` (Web) adds Serilog, which writes the console; `AddServiceDefaults()` adds OpenTelemetry's logging provider, which exports the same events to the Aspire dashboard when `OTEL_EXPORTER_OTLP_ENDPOINT` is set (Aspire sets it). That's the shape of [ardalis/CleanArchitecture](https://github.com/ardalis/CleanArchitecture)'s `LoggerConfigs`.
- **Levels:** the console's come from the `Serilog` section (Serilog ignores `Logging` for its own output), the dashboard's from `Logging:LogLevel`; keep them in step. ASP.NET Core, EF Core (every SQL command) and `System.Net.Http.HttpClient` (full URLs, query strings included) log at Warning and above. Lower one while debugging; don't commit it.
- **Nothing is redacted, so don't log what must not be read.** **What may be logged:** ids (`UserId`), names of operations, counts, durations, status codes and error codes. **What must not be logged:** emails, phone numbers, addresses, passwords, tokens and codes (antiforgery, confirmation, reset), API keys, cookies, request bodies, and whole commands, queries or DTOs (`{@Command}`, or a record's `ToString()`, prints every property). The full template has a redaction pipeline if a project needs one.
- `LoggingBehavior` logs every command and query by name, and how long it took, never its contents: registration and sign-in commands carry passwords, and a session carries tokens.
- **Diagnosing a failed request in the Aspire dashboard:**
  1. Note the failing request's route and time (error bodies carry no trace id, ADR 009).
  2. In the dashboard, open **Traces** and find the request by its route and time, or open **Structured logs** and filter on the request path.
  3. The trace shows every span: the API request, EF Core queries and outbound HTTP calls, with the failing one marked. Its logs, including any exception, are attached to the same trace.

## Rate limiting

- **ASP.NET Core's rate limiter** ([ADR 014]({{< relref "architecture-decisions/adr-014-rate-limiting-and-security-headers" >}}), `RateLimitingConfigurations`, the `RateLimiting` settings). A global fixed-window limiter counts a signed-in caller by `user_id` (600 a minute) and anyone else by address (120). The `authentication` policy (5 a minute per address) covers login, registration and the anonymous account endpoints, on top of Identity's lockout. A rejection is a 429 problem with `Retry-After`.
- **New sensitive endpoints get a policy.** Anything anonymous that sends email or is expensive gets the `authentication` policy, or a named one of its own, with `Options(x => x.RequireRateLimiting(name))` (cookbook, Limit one endpoint).
- **Partitions are never client-controlled.** Read `user_id` only from the validated session, and the client address from `X-Forwarded-For` only when the connection comes from a known proxy.
- **Don't use FastEndpoints' `Throttle(...)`.** It keys on the raw `X-Forwarded-For` header, so a client can rotate it to get a fresh limit.
- **No request timeout.** The template sets none; pass the `CancellationToken` through to EF Core and HttpClient calls so work stops when the client goes away. The full template has a global timeout if a project needs one.

## Metrics

- ServiceDefaults exports ASP.NET Core, HttpClient, runtime, Npgsql and EF Core metrics over OpenTelemetry; they show up in the Aspire dashboard's **Metrics** tab.
- For business metrics (users signed up, emails sent), add one class built on `System.Diagnostics.Metrics` with its own meter, register the meter in ServiceDefaults' `WithMetrics`, and call it from the handlers. The full template's `ApplicationMetrics` is the reference.
- **Count what happened, where it happened.** Increment right after the call that makes it true, so a failure never counts, and test the increment point with `MetricCollector<T>`.
- **Tags must stay bounded.** Never a user id, email, file name or anything else per-user or from input: every distinct value is a new time series.

## Health checks

- **Policy: Postgres is required.** Without it an instance can't serve anything.
- **Two probes**, mapped by `MapDefaultEndpoints()` in **Development only**, as Aspire's template does (the AppHost's `WithHttpHealthCheck("/health")` uses `/health`). They answer only with `Healthy`/`Unhealthy`:

  | Probe | Checks (tag) | Fails with | Use it for |
  |---|---|---|---|
  | `/alive` | the process itself (`live`) | 503 | liveness: restart the instance |
  | `/health` | every check: the process and Postgres (`ready`) | 503 | readiness: stop sending traffic |

- **In production,** map them deliberately (read [Aspire's note](https://aka.ms/dotnet/aspire/healthchecks) first), mark them `AllowAnonymous()`, and add them to `EndpointAuthorization`'s anonymous routes. Until then the fallback policy answers an anonymous request to them with 401.

- **Adding a dependency:** tag its check `ready` only if the app truly can't serve without it. An optional dependency (a cache, file storage) must not gate readiness, and the code that uses it must fail open; the full template reports those on a separate `/health/dependencies` probe. Never put a dependency in `live`: a database outage would restart every instance and make things worse.
- Give each check a short timeout (Postgres has 5 seconds), so a hanging dependency can't hang the probe.
- Aspire's client integrations add their own, untagged health checks, which would gate `/health`. Turn them off (`settings.DisableHealthChecks = true`) and register a tagged check instead.

## .NET Aspire

- `AppTemplate.ServiceDefaults` (wired into `Web` via `AddServiceDefaults()`) is where cross-cutting concerns (OpenTelemetry, health checks, service discovery, retries) belong - once for the whole app, not duplicated per-service.
- The AppHost is a local-orchestration and manifest-generation tool (see [ADR 002]({{< relref "architecture-decisions/adr-002-aspire-orchestration" >}})) - it is not itself a production runtime.

## Options

Configuration reaches code through registered, validated options, never through values invented where they're used.

- **Register before use.** `AddOptions<T>().Bind(section)` with explicit validation rules and `ValidateOnStart()`. `ValidateOnStart` only runs the rules you wrote: without a rule, a missing value is just the default.
- **No fallbacks at the call site.** `GetSection(...).Get<T>() ?? new T()` and `configuration["Key"] ?? "literal"` hide a misspelt section or a forgotten registration behind defaults that look like they work. Read `IOptions<T>` when the consumer is built instead: in a DI factory, or for framework options with `AddOptions<TFramework>().Configure<IOptions<T>>(...)`. Registration-time code only registers.
- **Three kinds of settings.**
  - *Required*: validated as present, and missing fails startup with the key in the message. `AccountLinks:ClientUrl` is one: its rule checks it is an absolute http(s) URL.
  - *Optional feature*: empty means off, by design (for example a storage service URL). When it's on, every setting is validated, so a half-configured feature fails instead of silently switching itself off.
  - *Safe defaults*: the default lives in the options class and the bound result is still validated. `appsettings*.json` keeps only the values we set.
- **Tested per composition root.** Each host's tests list the options it needs and assert they're registered and validated (`OptionsRegistrationTests`), plus startup tests for a missing or invalid required value (`StartupConfigurationTests`). A global reflection rule would force every options type into every host, which is the wrong boundary.

## Third-party configuration files

Configuration files for services we run but don't write (the full template's `AppHost/Garage/garage.toml` is an example) list every option the pinned version supports:

- Options in effect are set explicitly, even when the value equals the default, and marked `(default)`. An upgrade that changes a default then can't silently change our setup.
- Options we don't use stay commented out, showing their default and when you'd set them, so the file doubles as a reference for the version we actually run. The online reference often describes a newer release.
- The file names the version it was checked against. When the image tag is bumped, re-check every option and default; many services silently ignore unknown keys, so a renamed option wouldn't fail loudly.

This applies to third-party service configuration only. `appsettings*.json` keeps just the values we set.

## PostgreSQL & migrations

- `DatabaseConfigurations.ApplyMigrationsOnStartup` (and the automatic migration in `Development`) is a **local/demo convenience, not a production deployment strategy**. Auto-migrating on app startup means a bad migration blocks the app from starting at all, and with more than one replica, every instance races to apply migrations concurrently. For a real deployment, generate a [migration bundle](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying#apply-migrations-at-runtime) (`dotnet ef migrations bundle`) and run it as its own CI/CD step before the new app version starts.
- Index for the query patterns you actually have once the schema grows past the seed `User` table - missing indexes are the most common cause of slow Postgres queries, more often than anything on the EF Core side.
- Don't hold a DB connection open across an `await` that doesn't need it - it ties up a slot in Npgsql's connection pool for no reason.
