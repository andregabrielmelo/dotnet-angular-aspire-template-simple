---
title: "ADR 014: Rate limiting"
weight: 140
---

# ADR 014: Rate limiting

## Status
Accepted. The file name still says "security headers": an earlier version of this decision also had the API send response headers, and that part was dropped (see Consequences).

## Context
The API had no request limits. Identity's lockout protects one account at a time, but nothing stopped one client from guessing passwords across many accounts, registering in bulk, or making the account endpoints send email over and over.

## Decision
- **ASP.NET Core's rate limiter** (`AddRateLimiter`, `Web/Configurations/RateLimitingConfigurations`), not FastEndpoints' `Throttle`, which partitions by the raw `X-Forwarded-For` header a client can rotate.
  - **A global limiter** covers every request, in fixed one-minute windows (`RateLimiting` settings, `RateLimitSettings`). A signed-in caller counts against `user:{user_id}` (600 requests); an anonymous one against `ip:{address}` (120).
  - **The `authentication` policy** (`RateLimitingConfigurations.AuthenticationPolicy`, 5 attempts per address and minute) adds a stricter limit on top. It covers login, register, confirm-email, resend-confirmation, forgot-password and reset-password, applied with `Options(x => x.RequireRateLimiting(...))`.
  - **A rejection** is a 429 problem details response with `Retry-After`.
  - **Partitions never come from a header a client picks.** `user_id` is read from the validated session (`UseRateLimiter` runs after `UseAuthentication`).
  - **The address comes from `X-Forwarded-For`** only when the connection comes from a proxy in `ForwardedHeaders:KnownProxies`. The AppHost lists loopback in development (the `web:ForwardedHeaders:KnownProxies` array in its `appsettings.Development.json`, which it passes on as `ForwardedHeaders__KnownProxies__0`, `__1`), for the Angular dev server's proxy (which sends `X-Forwarded-For`). Elsewhere the list is empty until a deployment names its load balancer.
  - **With no known proxy, nothing is forwarded at all** (`ForwardedHeaders.None`). ASP.NET Core's middleware otherwise trusts every peer when both of its lists are empty, which would let any client choose its own partition. A functional test pins this.

## Consequences
- **Abuse is bounded per client, not stopped:**
  - A distributed attack from many addresses stays under the per-address limits. Lockout still protects each account, and an edge service (WAF, CDN) is the next step for real attack traffic.
  - The limiter is in memory, so per instance; with several instances, each counts separately. A distributed limiter (Redis) is the upgrade.
- **Deployments must list their proxies** in `ForwardedHeaders:KnownProxies`. Otherwise every client behind the proxy shares one anonymous partition (the proxy's address), and `X-Forwarded-Proto` is ignored.
- **Tests set their own limits.** The functional tests sign in many times from one address, so their factory raises the limits; `RateLimiting*` tests use a low-limit factory.
- **New sensitive endpoints** (anonymous forms, anything that sends email or is expensive) get the `authentication` policy or a named policy of its own.
- **No API security headers.** The API sets no response headers of its own (no CSP, `nosniff` or frame options). The host that serves the app and `/api` in production sends them (best-practices.md, Production deployment). HSTS stays (`UseHsts` outside Development).
- **`/_test_url_cache_`** (FastEndpoints 8.3, see ADR 012) is rate limited too, but still anonymous.
