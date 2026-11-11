---
title: "ADR 012: JWT authentication with FastEndpoints.Security"
weight: 120
---

# ADR 012: JWT authentication with FastEndpoints.Security

## Status
Superseded by [ADR 015]({{< relref "adr-015-cookie-sessions-aspnetcore-identity" >}}): sessions are ASP.NET Core Identity's cookie, and the API issues no JWTs. Previously: accepted. Supersedes [ADR 010]({{< relref "adr-010-authentication-api-issued-tokens" >}}), keeping its credentials, password hashing and one-origin setup. Amended by [ADR 013]({{< relref "adr-013-user-management-aspnetcore-identity" >}}): accounts, passwords and roles are now ASP.NET Core Identity's, which adds lockout; roles and permissions are smart enums (ADR 011).

## Context
ADR 010 had the API issue its own tokens, but built everything by hand: a token issuer, plain `AddJwtBearer`, token families with sliding and absolute expiry, `xmin`-guarded rotation with a 10-second reuse window and 409s, an `Origin` allow-list, antiforgery cookies and a sign-in rate limit. Each piece had a reason, but together they were more than a *simple* template should ask a new project to understand and own.

[FastEndpoints](https://fast-endpoints.com/docs/security), which the API already uses for every endpoint ([ADR 003]({{< relref "adr-003-fastendpoints-mediator" >}})), ships JWT support of its own: `AddAuthenticationJwtBearer`, `JwtBearer.CreateToken`, a `RefreshTokenService` that creates and renews token pairs and serves the refresh endpoint, `Permissions()` on endpoints, and a `JwtRevocationMiddleware`. Its demos ([Jwt-And-Identity-Auth-Demo](https://github.com/FastEndpoints/Jwt-And-Identity-Auth-Demo), [Refresh-Tokens-Demo](https://github.com/FastEndpoints/Refresh-Tokens-Demo)) show the intended shape. Following them means less code here, and documentation outside this repository for what remains.

## Decision

```
Browser ──(Authorization: Bearer <access token from localStorage>)──► Angular dev server /api ──► AppTemplate.Web
   └──(HttpOnly refresh cookie, sent only to /api/auth/refresh-token)──┘
```

- **FastEndpoints.Security, stable 8.3**, as its docs and demos use it:
  - `AddAuthenticationJwtBearer` validates tokens (HS256, the `Jwt` settings' issuer and audience, `MapInboundClaims = false`). A global `JwtCreationOptions` signs them.
  - **The middleware order** is `UseJwtRevocation`, then `UseAuthentication`, `UseAuthorization` and `UseFastEndpoints`.
  - **`UserTokenService : RefreshTokenService`** sets the lifetimes (15 minutes access, 7 days refresh) and serves `POST /auth/refresh-token`. It stores each refresh token (`PersistTokenAsync`), checks the presented one (`RefreshRequestValidationAsync`) and grants the renewed token the user's current claims (`SetRenewalPrivilegesAsync`).
  - **Login and register** check the credentials through a use case, then call `CreateTokenWith<UserTokenService, IssuedTokens>`.
  - **Claims:** `sub` (the `UserId`), `email`, `name`, a `jti`, and one `permissions` claim per permission ([ADR 011]({{< relref "adr-011-role-based-permissions" >}})), all added by `UserPrivilegesExtensions.AddUser`.
- **Settings live in appsettings** (`Jwt`: issuer, audience, signing key, both lifetimes and the cookie path), validated at startup (`JwtSettings`: a key of at least 32 characters, access shorter than refresh).
  - `appsettings.json` leaves the key empty, so a deployment without its own `Jwt__SigningKey` fails to start. `appsettings.Development.json` has a development-only key.
  - The settings are read once at registration, as FastEndpoints' setup expects.
- **The access token** is returned in the body (`AccessTokenResponse`: `accessToken`, `accessTokenExpiry`). The SPA keeps it in `localStorage`, so a reload or a new tab stays signed in, and sends it as `Authorization: Bearer`.
- **The refresh token** never appears in a body. It travels only as the `apptemplate_refresh` cookie: `HttpOnly`, `SameSite=Strict`, `Secure` outside Development, with its `Path` set to the refresh route (`/api/auth/refresh-token` as the browser sees it). The browser sends it nowhere else.
  - `IssuedTokens`, the service's `TokenResponse`, serializes as `AccessTokenResponse`, because FastEndpoints' own refresh endpoint sends the whole response object.
  - The database stores only its SHA-256 hash (`RefreshToken` aggregate), with a cascade from the user.
  - **Single use.** The token is deleted when used. Of two concurrent uses, the second delete affects no row and fails, so exactly one gets a new pair. Storing a new token also deletes that user's expired ones.
  - Other devices' sessions are untouched: each sign-in has its own token.
  - The renewed access token carries the user's current role, so a role change shows at the next refresh.
  - An invalid, used or expired token gets FastEndpoints' 400 and has its cookie deleted.
- **Logout** is `DELETE /auth/refresh-token`. It is authenticated with the Bearer token and sits on the cookie's route, so it gets both.
  - It deletes the refresh token and the cookie.
  - It also revokes the access token's `jti` until that token's own expiry: `RevokedAccessTokens` (an `IMemoryCache`) records it, and `RevokedTokenChecker`, a `JwtRevocationMiddleware`, answers 401 for it.
- **CSRF needs no token of its own.**
  - Only the refresh route receives a cookie, and `SameSite=Strict` keeps it off cross-site requests.
  - The refresh endpoint only accepts JSON, which a cross-site form can't send, and the API allows no CORS.
  - A forged refresh could at worst rotate the token, and its response can't be read cross-origin.
  - Every other endpoint needs the Bearer header, which a browser never adds by itself.
- **No authorization fallback policy.** FastEndpoints endpoints are secure by default (`AllowAnonymous()` is explicit), so the template relies on that. One route sits outside it: FastEndpoints 8.3 maps `GET /_test_url_cache_` outside its endpoint pipeline, without authorization metadata, and it lists every route and endpoint type name to anyone. Moving to FastEndpoints 8.4 or later, where the route is opt-in, closes it; until then, block it at the host or add a fallback policy (`AddAuthorization(o => o.FallbackPolicy = ...)`) if that listing matters.
- **The SPA** (`AuthService`) refreshes before sending a token that is about to expire, and on a 401 refreshes once and retries.
  - Refreshes are shared within a tab and serialized across tabs with the Web Locks API (`REFRESH_LOCK`). Inside the lock, a tab first looks in `localStorage`: if another tab has just refreshed, it uses that token rather than spending the already rotated cookie.
  - A `storage` listener keeps tabs in step.
  - `logout()` refreshes an expired token first, so the API can always revoke the session.
- **Unchanged from ADR 010:** credentials, password hashing and the indistinguishable 401 for an unknown email or a wrong password; one origin through the dev server proxy; the welcome email on registration.

## Consequences
- **Far less code.** No token issuer, families, reuse window, 409 retries, `Origin` checks, antiforgery or rate limit policies. `UserTokenService` overrides four hooks, and the use cases (`Login`, `StoreRefreshToken`, `ConsumeRefreshToken`, `Logout`) are small enough to read in one sitting.
- **XSS can read the access token** from `localStorage`, for up to 15 minutes, and could use it from elsewhere during that time. It still can't read the refresh token. Keep the strict CSP and never render untrusted HTML.
- **Revocation is per instance and in memory.** A restart, or another instance behind a load balancer, doesn't know about a revoked access token. At worst, a logged-out token keeps working until it expires (15 minutes), which is what short-lived JWTs give anyway. Move `RevokedAccessTokens` to `IDistributedCache` when the API runs on several instances.
- **No sliding or absolute session limit.** Each refresh issues a new 7-day token, so a session used at least once a week never ends on its own. Shorten `RefreshTokenValidity`, or store a session start on the token, if that matters.
- **No refresh-token reuse detection.** A stolen refresh token used before its owner gets a new pair. The owner's next refresh then fails, and they sign in again; their new session doesn't revoke the thief's. ADR 010's token families detected this; bring them back if a project needs it.
- **No brute-force limit** on login. Add ASP.NET Core rate limiting, or lockout, before going to production with public sign-up.
- **Web clients only.** The refresh token is only ever in the cookie. A mobile or server client would need a body-based refresh flow, which isn't included.
- **The refresh request's documented body** is FastEndpoints' `TokenRequest` (`userId`, `refreshToken`). The endpoint ignores both, and the web app posts `{}`.
- **Development-only key in a committed file.** It signs only local tokens. Every other environment must set `Jwt__SigningKey`, or startup fails.
- **Tests sign in for real.** There is no test authentication handler. Functional tests get tokens from the real endpoints (`TestSession`), and `AccessTokenValidation` checks that a wrong key, issuer or expiry is rejected.
