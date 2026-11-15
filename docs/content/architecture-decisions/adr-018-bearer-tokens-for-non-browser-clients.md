---
title: "ADR 018: Bearer tokens for non-browser clients, next to the session cookie"
weight: 180
---

# ADR 018: Bearer tokens for non-browser clients, next to the session cookie

## Status
Accepted. Adds to [ADR 015]({{< relref "adr-015-cookie-sessions-aspnetcore-identity" >}}), which stays the browser's way in.

## Context
The Angular app signs in with Identity's session cookie (ADR 015): HttpOnly, `SameSite=Strict`, with an antiforgery token for unsafe requests. A browser holds it without the app ever seeing a credential. Clients that aren't a browser on the app's origin can't use it well: a script, a mobile or desktop app, another service. They need a credential they send themselves.

[NimblePros/eShopOnWeb](https://github.com/NimblePros/eShopOnWeb) does this with an `ITokenClaimsService` that issues a JWT after a password check (`IdentityTokenClaimService`), for its separate public API, and a `JwtSettingsValidator` that refuses weak or development-only keys. This template had JWTs before ([ADR 012]({{< relref "adr-012-jwt-authentication-fastendpoints" >}})) and replaced them for the browser, because a token the SPA holds can be stolen by any script on the page and needs refresh and revocation machinery. Those reasons apply to the SPA, not to a client that has no page to inject into.

## Decision
- **`POST /auth/token`** (anonymous, with the `authentication` rate limit) checks the credentials exactly as `POST /auth/login` does: lockout, the same 401 for any refusal, 403 for an unconfirmed email (`CreateTokenHandler`). It returns `{ accessToken, tokenType: "Bearer", expiresAt }` instead of setting a cookie.
- **The token service is a port.** `ITokenClaimsService` (UseCases) is implemented by `IdentityTokenClaimService` (Infrastructure). It signs an HS256 JWT (`JsonWebTokenHandler`, the successor of eShop's `JwtSecurityTokenHandler`) with the session's own claims: it calls the session's claims factory (`ApplicationClaimsPrincipalFactory`), so a token authorizes exactly what the cookie does, `user_id`, roles and `permissions` included. The security stamp is left out: nothing checks it in a token, and a token is readable.
- **One default scheme picks the credential.** A policy scheme (`TokenAuthentication`) is the default for authenticating, challenging and forbidding: a request with `Authorization: Bearer ...` goes to JwtBearer, any other to the cookie. Everything downstream (`ICurrentUser`, `Permissions(...)`, the rate limiter's partitions, user context in traces and logs) reads the same claims either way. Signing in and out, and the stamp checks, stay on the cookie scheme.
- **No antiforgery for bearer requests.** A browser never attaches an `Authorization` header by itself, so a bearer request can't be forged cross-site; `UseSessionAntiforgery` checks only cookie sessions.
- **Settings (`Authentication:Jwt`, `JwtSettings`), validated at startup** (`JwtSettingsValidator`): `SigningKey` (at least 32 bytes), `Issuer`, `Audience`, `TokenLifetimeMinutes` (15 by default, 1 to 1440). In Development with no key set, each process makes up a random one (`DevelopmentJwtSigningKey`). That replaces eShop's fixed development key: there's no key in the code to leak into production, and nothing for the secret scan to flag. Elsewhere the key is required, so a missing one stops startup. The AppHost passes it outside Development as the secret parameter `jwt-signing-key`.
- **Lifetimes are checked against `TimeProvider`**, like the cookie's, with 30 seconds of clock skew.

## Consequences
- **No revocation before expiry.** Signing out, changing the password, a reset or a role change don't affect tokens already issued: unlike the cookie, a token isn't rechecked against the security stamp. The lifetime is short for that reason; keep it short.
- **No refresh tokens.** When a token expires, the client asks for a new one with the password. A client that can't keep the password needs refresh tokens or an external identity provider (the full template's Keycloak); add them with their own ADR.
- **Every instance needs the same key**, and rotating it invalidates every token at once. Keep it in the platform's secret store (`Authentication__Jwt__SigningKey`, [Deploying to production]({{< relref "guides/deploying" >}})).
- **The browser keeps the cookie.** The Angular app never calls `/auth/token`, and must not store a token (AGENTS.md).
- The OpenAPI document declares the bearer scheme, so the API reference can send a token.
- Development tokens don't survive an API restart, since the random key changes.
