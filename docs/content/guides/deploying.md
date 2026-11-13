---
title: "Deploying to production"
weight: 40
---

# Deploying to production

The AppHost runs everything locally ([ADR 002]({{< relref "architecture-decisions/adr-002-aspire-orchestration" >}})); it isn't how you run in production. This guide covers what a production host needs, whichever platform you choose (a VM with a reverse proxy, containers behind an ingress, a PaaS). The short rules live in [Best practices: Production deployment]({{< relref "best-practices#production-deployment" >}}); this page is the step-by-step version.

## The shape

```
                   Browser
                      │  HTTPS, one origin: https://app.example.com
                      ▼
          Reverse proxy / ingress  ── terminates TLS, sets the security headers
             │                 │
     /  (everything else)    /api/*  (strip /api)
             │                 │
             ▼                 ▼
   Angular build (static)   ASP.NET Core API (AppTemplate.Web)
                               │
             ┌─────────────────┼──────────────┬──────────────────┐
             ▼                 ▼              ▼                  ▼
         PostgreSQL       Redis (optional)   SMTP relay    OpenTelemetry collector
     (data + Data         (HybridCache L2)                 (logs, traces, metrics)
      Protection keys)
```

The SPA and the API share **one origin**. The session cookie is `SameSite=Strict` and the API has no CORS, so the browser must see `/api` on the same scheme, host and port as the app. The proxy strips `/api` before forwarding, exactly as the Angular dev server's proxy does (`frontend/proxy.conf.mjs`). The [best practices]({{< relref "best-practices#production-deployment" >}}) describe the same-site alternative (`app.` and `api.` subdomains) and why fully cross-site isn't supported.

## 1. Build the artifacts

- **The API:** `dotnet publish backend/src/AppTemplate.Web -c Release -o out/api`, or a container image (`dotnet publish ... /t:PublishContainer`). It listens on HTTP behind the proxy (`ASPNETCORE_URLS=http://+:8080`); the proxy speaks HTTPS to the browser.
- **The SPA:** `npm ci && npm run build` in `frontend/`. Serve `frontend/dist/frontend/browser/` as static files, falling back to `index.html` for client-side routes.
- Build both from the same commit: the Angular types are generated from the API's committed OpenAPI document (`npm run api:generate`).

## 2. Configure the reverse proxy

- **Routes:** `/api/*` goes to the API with the `/api` prefix removed; everything else goes to the static files.
- **TLS** ends at the proxy. Redirect HTTP to HTTPS there, and send `Strict-Transport-Security`.
- **Forwarded headers:** the proxy sets `X-Forwarded-For` and `X-Forwarded-Proto`. The API honors them only from the addresses in `ForwardedHeaders__KnownProxies__0`, `__1`, ... (the proxy's IPs as the API sees them). Without that, every client shares one rate-limit partition, and the API thinks requests arrived over HTTP.
- **Security headers** for both the SPA and `/api`: the CSP from `angular.json` (`frontend/angular.json`, the dev server's `headers`), `X-Content-Type-Options: nosniff`, `Referrer-Policy`, `X-Frame-Options`/`frame-ancestors`. The API sends none of its own.
- **Block `/_test_url_cache_`** at the proxy (FastEndpoints 8.3 maps it anonymously; [ADR 012]({{< relref "architecture-decisions/adr-012-jwt-authentication-fastendpoints" >}})).

## 3. Settings and secrets

Everything is an environment variable (`Section__Key`); `appsettings.json` only holds log levels. Required settings are validated at startup, so a missing one stops the API with the setting's name in the error.

| Setting | Required | Notes |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | yes | `Production`. Never `Development`: it seeds demo accounts and maps the health probes and API reference without protection. |
| `ConnectionStrings__apptemplate` | yes | Postgres. A secret: keep it in your platform's secret store, not in an image or a file in the repository. |
| `ConnectionStrings__cache` | no | Redis for HybridCache's L2. Without it, each instance caches in memory only. With several instances, set it, or another instance may serve a stale entry for up to the local lifetime. |
| `Mailserver__Hostname`, `Mailserver__Port`, `Mailserver__FromAddress` | yes | The sender connects without credentials (`MimeKitEmailSender`, `SecureSocketOptions.Auto`). Use a relay that accepts the API, or add a user name and password to `MailserverConfiguration` first. |
| `AccountLinks__ClientUrl` | yes | The public URL of the app (`https://app.example.com`). Confirmation and reset links open it. |
| `ForwardedHeaders__KnownProxies__0`, ... | behind a proxy | The proxy's addresses. |
| `RateLimiting__*` | no | Defaults: 600/min per user, 120/min per address, 5/min on the account endpoints. They count per instance. |
| `Caching__Expiration`, `Caching__LocalExpiration`, `Caching__Profiles__{Name}__*` | no | Cache lifetimes ([ADR 017]({{< relref "architecture-decisions/adr-017-hybrid-cache" >}})). |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | no | Sends logs, traces and metrics to an OpenTelemetry collector (`ServiceDefaults`). |
| `Database__ApplyMigrationsOnStartup` | no | Leave it off in production (next step). |

## 4. Migrate the database

Migrations run on startup only in Development, or when `Database__ApplyMigrationsOnStartup=true`. In production, apply them as a **separate deployment step, before the new API version starts**:

- A migration bundle, built with the release:
  ```bash
  dotnet ef migrations bundle --project backend/src/AppTemplate.Infrastructure \
    --startup-project backend/src/AppTemplate.Web -o out/migrate --self-contained
  ```
  Run `out/migrate --connection "<connection string>"` from your pipeline.
- Or an idempotent SQL script for a DBA to review and apply: `dotnet ef migrations script --idempotent ...`.

Write migrations so the previous API version still works against the new schema (add before you remove), because old and new instances overlap during a rolling deployment. Seed data never runs outside Development.

## 5. Health probes

The probes (`/alive`, `/health`) are mapped only in Development. To use them for your platform's liveness and readiness checks, map them in production on purpose: mark them `AllowAnonymous()`, add them to `EndpointAuthorization`'s anonymous routes, and keep them on an internal port or path that the proxy doesn't expose ([Best practices: Health checks]({{< relref "best-practices#health-checks" >}})). Redis has no health check: it's optional, and the API keeps serving reads without it.

## 6. Observe it

- **Logs** go to the console (Serilog) and, with `OTEL_EXPORTER_OTLP_ENDPOINT`, to your collector. Nothing is redacted, which is why the code never logs request or response contents.
- **Traces and metrics** (ASP.NET Core, HttpClient, EF Core, Npgsql, runtime) go to the same collector.
- **Per user:** a signed-in request's trace carries a `user.id` tag, and its logs a `UserId` property (`UserContextEnrichmentMiddleware`), so you can follow one user's requests. It's the profile id, not the email or the name.

## 7. Back up

- **Postgres** holds everything that matters: profiles, accounts, and the `data_protection_keys` table. Back it up on a schedule (`pg_dump`, or your provider's point-in-time recovery) and test a restore.
- **The Data Protection keys** protect session cookies and the emailed codes. Losing them signs everyone out and invalidates outstanding links. Anyone who can read them can forge both, so restrict access to the table and its backups.
- **Redis** needs no backup: it only holds cache entries, which rebuild themselves.

## Before going live

- [ ] `ASPNETCORE_ENVIRONMENT=Production`, and every required setting is set (startup fails otherwise).
- [ ] The SPA and `/api` are served from one origin over HTTPS, with the security headers.
- [ ] `ForwardedHeaders__KnownProxies__*` lists the proxy.
- [ ] Migrations ran before the API started.
- [ ] `/_test_url_cache_` is blocked.
- [ ] Postgres backups run, and a restore has been tested.
