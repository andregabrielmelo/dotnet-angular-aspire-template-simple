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
- Inject `ILogger<T>`, never call the static `Serilog.Log` directly (it isn't configured). Use message templates (`"{UserId} created", id`), not string interpolation - it's what makes structured log fields searchable, and interpolated text bypasses redaction. Change log levels in `appsettings.json`'s `Serilog:MinimumLevel` section; the pipeline itself is in `ServiceDefaults/Logging/` (see Logging below).
- Tests follow Arrange-Act-Assert, one behavior per test. See `AppTemplate.UnitTests` for the current shape.
- Endpoints require an authenticated user (a Keycloak JWT) by default. Only add `AllowAnonymous()` deliberately. Protect administrative operations with a permission policy (`Policies(Permission.X)`), and put rules that depend on the loaded resource (ownership) in the use case via `ICurrentUser` (see [ADR 008]({{< relref "architecture-decisions/adr-008-permission-based-authorization" >}})). Read the caller's identity from JWT claim names (`sub`, `email`, `name`), since inbound claim mapping is off. Never put tokens in the browser: the Angular app relies on the backend for frontend's HTTP-only cookie (see [ADR 007]({{< relref "architecture-decisions/adr-007-authentication-backend-for-frontend-keycloak" >}})).

## Angular

- Feature-folder structure (`core/`, `shared/`, `features/<feature>/`) is already the convention here - keep new code inside it rather than growing `components/`/`services/`-by-type folders. See the [official style guide](https://angular.dev/style-guide).
- Hyphenate file names, one concept per file, co-locate a component's `.ts`/`.html`/`.css`/`.spec.ts`.
- Prefer `inject()` at field-initializer time over constructor-parameter injection for new code - it reads better and infers types more reliably.
- For state: local component state first; a signal exposed (read-only, via `computed()`) from a service when a few components need to share it; a route-scoped feature store only once that's not enough; NgRx SignalStore only once a feature genuinely needs a full store. Don't start with a global store.
- Prefer native `[class]`/`[style]` bindings over `NgClass`/`NgStyle`.
- Tests run on Vitest (already the default here) - keep specs next to the file they test, as `app.spec.ts` already does.
- **HTTP lives in feature services.** Each feature has a `<feature>.service.ts` that owns its API calls and a `<feature>.model.ts` for the response shapes (see `features/users/`). Components call the service and never inject `HttpClient` - ESLint fails the build if they do.
- **Guards are UX, not security.** `authGuard` and `permissionGuard` decide what to *show*; the API enforces every permission on its own. Never treat a hidden route as protected data.
- **Errors are problem details** ([ADR 009]({{< relref "architecture-decisions/adr-009-problem-details-error-contract" >}})). Branch on `HttpErrorResponse.status` for the message, and for a 400 read `error.error.errors` - an object of camelCase field name to messages, matching the form control names - to show each message next to its field. Never render a raw error body.
- **Strict typing is on** (`strict` and `strictTemplates` in `tsconfig.json`). Don't loosen it per file, and avoid `any`; give API responses an interface in the feature's model file.
- **Accessibility:** semantic elements, a `<label>` for every control, keyboard operation, and error text linked to its field with `aria-describedby`. `npm run lint` runs Angular's template accessibility rules - fix what it reports rather than disabling the rule.

## Security headers

- **The SPA document** (served by the backend for frontend, from `wwwroot` or the Angular dev server) gets a strict `Content-Security-Policy` (`SecurityHeadersMiddleware.ContentSecurityPolicy`), `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin` and `X-Content-Type-Options: nosniff`. Scripts load only from the same origin: no inline `<script>`, no inline event handlers, no `eval`. Styles allow `'unsafe-inline'`, because Angular injects component styles at runtime.
- **Keep the build CSP-compatible.** Angular's critical CSS inlining is off in `angular.json`, because it adds an `onload` handler the CSP blocks, which would leave the page unstyled. `npm run csp:check` (run in CI after the build) fails if the built `index.html` contains an inline script or handler. If you add a third-party script, font or image host, add its origin to the matching CSP directive instead of loosening `script-src`.
- **The API** (`UseApiSecurityHeaders` in Web) only returns JSON, so it sends `X-Content-Type-Options: nosniff` on every response, and `Cache-Control: no-store` on every response for a signed-in caller, so browsers and shared proxies never keep one user's data.
- **No response compression.** Compressing HTTPS responses that mix secrets with attacker-influenced input enables [BREACH](https://www.breachattack.com/)-style attacks, and the reverse proxy or CDN in front of a real deployment compresses static assets better anyway. Don't add `UseResponseCompression()` to either host.

## Logging

- **One pipeline, two outputs.** Every host calls `AddServiceDefaults()`, which makes Serilog the only logging provider (`ServiceDefaults/Logging/LoggingExtensions.cs`). It writes to the console and, when `OTEL_EXPORTER_OTLP_ENDPOINT` is set (Aspire sets it), exports the same events over OTLP to the Aspire dashboard. Redaction runs before both, so the console and the dashboard can never disagree about what was hidden.
- **One line per request** (`UseDefaultRequestLogging`): method, path (never the query string), status, duration, `EndpointName`, the caller's `Subject` (`sub`), and the trace and span ids. Request and response bodies and headers (`Authorization`, `Cookie`) are never logged. Health probes log at Verbose.
- **Quiet by default:** ASP.NET Core, EF Core (every SQL command), `System.Net.Http.HttpClient` (full URLs, query strings included) and YARP log at Warning and above. Lower one in `Serilog:MinimumLevel:Override` while debugging; don't commit it.
- **What may be logged:** ids (`UserId`, `sub`), names of operations, counts, durations, status codes and error codes. **What must not be logged in clear text:** emails, phone numbers, addresses, passwords, tokens (access, refresh, reset), API keys, cookies, and request bodies.
- **How redaction works**, in three layers:
  1. **Classification.** Mark the type (`[PersonalData] class EmailAddress`) or the property (`[property: PersonalData] string? PhoneNumber` on a record) with `[PersonalData]` or `[SecretData]` from SharedKernel (`Microsoft.Extensions.Compliance` data classifications). When the object is destructured (`{@Command}`), those values go through the configured redactor (`PlaceholderRedactor`, which writes `[redacted]`; swap in an HMAC redactor in `AddDefaultLogging` if you need to correlate values without revealing them).
  2. **Property names.** Any string property named like `Email`, `Phone...`, `...Token`, `Password`, `Secret`, `Authorization`, `Cookie` or `ApiKey`, including scope properties and nested ones, is replaced.
  3. **Patterns.** Every other string has emails, JWTs and `Bearer` tokens masked.
- **Log classified objects with `@`.** Without it, Serilog captures `ToString()`, which loses the type: only the property name and pattern layers apply then. `{Phone}` is still caught by its name; `{Value}` holding a phone number is not.
- `LoggingBehavior` logs every command and query with `{@Request}` (so classified properties are redacted) and how long it took. It deliberately doesn't log the response.
- **Diagnosing a failed request in the Aspire dashboard:**
  1. The client gets a problem details body with a `traceId` (ADR 009).
  2. In the dashboard, open **Traces** and search for that trace id, or open **Structured logs** and filter on `TraceId`.
  3. The trace shows every span: the backend for frontend's proxy call, the API request, EF Core queries and outbound HTTP calls, with the failing one marked. Its logs, including the request line (`HTTP PUT /users/7 responded 500`) and any exception, are attached to the same trace.
  4. Locally the console shows the same trace and span ids at the end of each line, so you can also `grep` for them.

## Rate limiting and timeouts

- **A global limiter, plus named policies when needed.** ASP.NET Core's rate limiter (`RateLimitingConfigurations`) has a **global** limiter for overall traffic (600 requests a minute per signed-in user, 120 per anonymous address, set under `RateLimiting`). A sensitive or expensive endpoint (an anonymous form that sends email, an upload) gets a stricter **named policy**, added with `options.AddPolicy` next to the global one and applied with `Options(x => x.RequireRateLimiting(name))`. Every rejection is a 429 problem details response with `Retry-After`. Health endpoints are never limited.
- **Partitions are never client-controlled.** A signed-in caller counts against `user:{sub}`, read only after JWT validation. Anyone else counts against `ip:{address}`, and that address comes from `X-Forwarded-For` only when the connection is from a proxy listed in `ForwardedHeaders:KnownProxies`/`KnownNetworks`. The list is empty by default; `appsettings.Development.json` trusts loopback for the local backend for frontend. In production, list your load balancer or reverse proxy, or every caller behind it shares one anonymous partition.
- **Don't use FastEndpoints' `Throttle(...)`.** It keys on the raw `X-Forwarded-For` header, so a client can rotate it to get a fresh limit, and its 429 is plain text, not problem details.
- **Request timeouts.** Every request gets 30 seconds (`RequestTimeouts:Default`), after which `HttpContext.RequestAborted` is cancelled and the client gets a 504 problem details response. Pass the `CancellationToken` through to EF Core and HttpClient calls so the work actually stops. An endpoint that waits on an external service with retries needs longer than the HttpClient's resilience total timeout (30 seconds): add a named policy in `RequestTimeoutConfigurations` and apply it with `Options(x => x.WithRequestTimeout(name))`. Timeouts are disabled while a debugger is attached.
- **A timeout doesn't undo anything.** If the transaction already committed, or an email or HTTP call already went out, it stays done even though the client got a 504. Make writes safe to retry, or make them idempotent.

## Metrics

- ServiceDefaults exports ASP.NET Core, HttpClient, runtime, Npgsql and EF Core metrics over OpenTelemetry; they show up in the Aspire dashboard's **Metrics** tab.
- For business metrics (users signed up, emails sent), add one class built on `System.Diagnostics.Metrics` with its own meter, register the meter in ServiceDefaults' `WithMetrics`, and call it from the handlers. The full template's `ApplicationMetrics` is the reference.
- **Count what happened, where it happened.** Increment right after the call that makes it true, so a failure never counts, and test the increment point with `MetricCollector<T>`.
- **Tags must stay bounded.** Never a user id, email, file name or anything else per-user or from input: every distinct value is a new time series.

## Health checks

- **Policy: Postgres is required.** Without it an instance can't serve anything.
- **Two probes**, mapped by `MapDefaultEndpoints()` in every environment, anonymous, excluded from rate limiting, tracing and request logs (Verbose). They answer only with `Healthy`/`Unhealthy`:

  | Probe | Checks (tag) | Fails with | Use it for |
  |---|---|---|---|
  | `/alive` | the process itself (`live`) | 503 | liveness: restart the instance |
  | `/health` | required dependencies (`ready`): Postgres | 503 | readiness: stop sending traffic |

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
  - *Required*: validated as present, and missing fails startup with the key in the message. Plain required values (the Keycloak realm, audience and client id) use `GetRequiredValue`, read at registration so they fail at startup rather than on the first request.
  - *Optional feature*: empty means off, by design (for example a storage service URL). When it's on, every setting is validated, so a half-configured feature fails instead of silently switching itself off.
  - *Safe defaults*: the default lives in the options class and the bound result is still validated. `appsettings*.json` keeps only the values we set.
- **Tested per composition root.** Each host's tests list the options it needs and assert they're registered and validated (`OptionsRegistrationTests`), plus startup tests for a missing required value (`StartupConfigurationTests`, the backend for frontend's `ConfigurationTests`). A global reflection rule would force every options type into every host, which is the wrong boundary.

## Third-party configuration files

Configuration files for services we run but don't write (the full template's `AppHost/Garage/garage.toml` is an example) list every option the pinned version supports:

- Options in effect are set explicitly, even when the value equals the default, and marked `(default)`. An upgrade that changes a default then can't silently change our setup.
- Options we don't use stay commented out, showing their default and when you'd set them, so the file doubles as a reference for the version we actually run. The online reference often describes a newer release.
- The file names the version it was checked against. When the image tag is bumped, re-check every option and default; many services silently ignore unknown keys, so a renamed option wouldn't fail loudly.

This applies to third-party service configuration only. `appsettings*.json` keeps just the values we set, and the Keycloak realm file is an export.

## PostgreSQL & migrations

- `DatabaseConfigurations.ApplyMigrationsOnStartup` (and the automatic migration in `Development`) is a **local/demo convenience, not a production deployment strategy**. Auto-migrating on app startup means a bad migration blocks the app from starting at all, and with more than one replica, every instance races to apply migrations concurrently. For a real deployment, generate a [migration bundle](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying#apply-migrations-at-runtime) (`dotnet ef migrations bundle`) and run it as its own CI/CD step before the new app version starts.
- Index for the query patterns you actually have once the schema grows past the seed `User` table - missing indexes are the most common cause of slow Postgres queries, more often than anything on the EF Core side.
- Don't hold a DB connection open across an `await` that doesn't need it - it ties up a slot in Npgsql's connection pool for no reason.
