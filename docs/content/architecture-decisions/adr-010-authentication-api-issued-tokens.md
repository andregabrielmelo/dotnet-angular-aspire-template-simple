---
title: "ADR 010: The API issues access and refresh tokens"
weight: 100
---

# ADR 010: The API issues access and refresh tokens

## Status
Superseded by [ADR 012]({{< relref "adr-012-jwt-authentication-fastendpoints" >}}), which keeps the credentials, password hashing and one-origin setup and replaces the rest with FastEndpoints.Security. Superseded [ADR 007]({{< relref "adr-007-authentication-backend-for-frontend-keycloak" >}}).

## Context
ADR 007 made Keycloak the identity provider and put a backend for frontend (a YARP host owning an HTTP-only session cookie) between the browser and the API. That is the strongest browser setup, but for the *simple* template it is a lot to carry: a Keycloak container and realm file, a second ASP.NET Core host with its own tests, OpenID Connect, token management, and fixed ports tied to redirect URIs. Most projects starting from this template want sign-up and sign-in with an email and a password, and an API they can also call from other clients.

We want the template to stay simple to understand without being weak on security. The full template ([dotnet-angular-aspire-template](https://github.com/andregabrielmelo/dotnet-angular-aspire-template)) keeps ADR 007's design for projects that need it.

## Decision

```
Browser ──(Authorization: Bearer <access token>)──► Angular dev server /api ──► AppTemplate.Web
   └──(HttpOnly refresh cookie, POST /api/auth/refresh)──┘
```

- **Credentials.** `POST /auth/register` and `POST /auth/login` take an email and a password. Passwords are hashed with ASP.NET Core Identity's `PasswordHasher` (PBKDF2, per-password salt) behind `IPasswordHasher` (UseCases; implemented in Infrastructure), without the rest of ASP.NET Core Identity. The hash is stored on the domain `User`. An unknown email and a wrong password get the same 401, and an unknown email still pays for a hash, so neither the answer nor its timing reveals who has an account.
- **Access token.** A 15-minute HS256 JWT (`Web/Authentication/JwtTokenIssuer`) with `iss`, `aud`, `sub` (the `UserId`), `iat`, `exp`, `jti`, `email`, `name` and one `permission` claim per permission ([ADR 011]({{< relref "adr-011-role-based-permissions" >}})). The API validates it with ASP.NET Core's `AddJwtBearer`, every check explicit: signature, `HS256` only, issuer, audience and lifetime (30-second clock skew). The signing key is base64 of at least 32 random bytes; startup fails without one. The AppHost generates it for Development and keeps it in user secrets; elsewhere it is the `jwt-signing-key` parameter.
- **Refresh token.** 64 random bytes, sent only as an `HttpOnly`, `SameSite=Strict` cookie (`Secure` outside Development) whose path is `/api/auth`, so it reaches nothing but the auth endpoints. The database stores only its SHA-256 hash (`RefreshToken` aggregate).
  - **Families.** Every sign-in starts a family; every refresh rotates the token within it. A user signed in on a laptop and a phone has two families.
  - **Expiry.** 7 days sliding (each rotation renews it) and 30 days absolute per family, so even a constantly used session ends.
  - **Atomic rotation.** Revoking the old token and inserting the next one is one `SaveChanges`, and Postgres' `xmin` row version makes the revocation conditional: of two requests rotating the same token, exactly one wins; the other gets a 409.
  - **Reuse detection.** A token rotated within the last 10 seconds is answered with a 409 (another tab refreshed with the same cookie at the same moment), and the client retries with the cookie it now holds. A rotated token presented later is a replay: its whole family is revoked, and only that family.
  - `POST /auth/logout` revokes the family and deletes the cookie. Deleting a user deletes their tokens (cascade).
- **CSRF.** Refresh and logout are authenticated by the cookie, so SameSite alone isn't trusted (a sibling subdomain is the same site). They also require an `Origin` from `Authentication:AllowedOrigins` and ASP.NET Core antiforgery: `GET /auth/antiforgery` (and every sign-in) sets a readable `XSRF-TOKEN` cookie, which Angular's built-in XSRF support echoes as `X-XSRF-TOKEN`.
- **Brute force.** Login and registration have their own rate limit policy, 5 attempts a minute per client address, on top of the global limiter.
- **The SPA** keeps the access token in memory only. After a reload, `ensureSession()` gets a new one from the refresh cookie. A 401 from the API triggers one refresh and a retry; refreshes are shared within a tab and serialized across tabs with the Web Locks API. The SPA never decodes the token.
- **One origin.** The Angular dev server proxies `/api` to the API, so there is no CORS and the cookie is first-party. In production, serve the built app and `/api` from one host.
- **Registration emails.** `UserRegisteredDomainEvent` (carrying the new `UserId` and email) sends a welcome email, best-effort.

## Consequences
- **No identity provider features.** No password reset, email verification, account lockout, 2FA or external sign-in. This is minimal authentication, not identity management. Add them yourself, or take ADR 007's Keycloak setup from the full template.
- **XSS** can't read the refresh token, but script running in the page can use the in-memory access token while the page is open. Keep the strict CSP and never render untrusted HTML.
- **Stateless access tokens.** A role change or a deleted user reaches the user with their next access token: an already issued token stays valid for up to 15 minutes. Refresh tokens are checked on every use, so revoking them is immediate.
- **Cookie scope.** With `SameSite=Strict`, a same-origin deployment works as is, and `app.example.com` with `api.example.com` works with CORS for that exact origin with credentials. A fully cross-site SPA and API needs a different cookie policy (`SameSite=None; Secure`) and is not supported as is.
- **The rate limiter is in memory**, so per instance. Behind several instances, add a distributed limiter or account-level throttling.
- **Refresh tokens accumulate.** Delete rows whose `family_expires_at_utc` has passed, for example with a scheduled SQL job. Never delete revoked tokens earlier: reuse detection needs them while their family is alive.
- **A logout racing a refresh** of the same session can lose (409); the client clears its state either way, and the token that refresh issued expires on its own.
- **Tests.** `AuthEndpointsTests` and `AuthSessionLifetimeTests` drive sign-in, rotation, concurrent refreshes, reuse, expiry and the CSRF checks through the real JWT handler; `TestAuthHandler` still stands in for a token everywhere else.
