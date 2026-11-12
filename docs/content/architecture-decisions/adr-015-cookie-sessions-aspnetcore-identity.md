---
title: "ADR 015: Cookie sessions with ASP.NET Core Identity"
weight: 150
---

# ADR 015: Cookie sessions with ASP.NET Core Identity

## Status
Accepted. Supersedes [ADR 012]({{< relref "adr-012-jwt-authentication-fastendpoints" >}}): the API no longer issues JWTs or refresh tokens. Amends [ADR 013]({{< relref "adr-013-user-management-aspnetcore-identity" >}}) (Identity now also runs the session) and [ADR 011]({{< relref "adr-011-role-based-permissions" >}}) (permissions travel in the cookie's claims).

## Context
ADR 012 signed users in with FastEndpoints.Security JWTs. Around them the template owned a lot of code:
- a `RefreshToken` aggregate (hash, security stamp, single use)
- four refresh use cases, and `UserTokenService`
- the refresh cookie on its own path
- in-memory access token revocation for logout
- the claim-building extensions
- on the frontend: a token store in `localStorage`, an interceptor that refreshed before expiry and after a 401, and a cross-tab Web Lock

ASP.NET Core Identity already does all of that for a same-origin SPA, which is what Microsoft recommends there ([Identity for SPAs](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization)).

## Decision
- **Identity's application cookie is the session.**
  - `AddIdentity<ApplicationUser, IdentityRole<int>>` registers the cookie schemes and makes `Identity.Application` the default scheme.
  - `POST /auth/login` checks the password (with lockout, unchanged), then calls `SignInManager.SignInAsync(isPersistent: true)`.
  - `POST /auth/logout` calls `SignOutAsync`.
  - Endpoints stay FastEndpoints classes, which use the default scheme; Web still never references Identity, which it reaches through `IIdentityService`.
- **The cookie** (`ConfigureApplicationCookie`, Infrastructure):
  - Identity's default name (`.AspNetCore.Identity.Application`) and lifetime (14 days, sliding), `HttpOnly`
  - `SameSite=Strict`, and always `Secure` (ADR 016)
  - `OnRedirectToLogin` answers 401 and `OnRedirectToAccessDenied` 403, instead of redirecting to Identity's login pages
  - it is protected with the Data Protection keys in Postgres (ADR 013), so sessions survive restarts and work on every instance
- **Claims come from Identity's claims factory.** `ApplicationClaimsPrincipalFactory` adds two things to Identity's own claims (account id, email, security stamp and `role` claims):
  - `user_id`, the profile's `UserId`, which `ICurrentUser` and the rate limiter read
  - one `permissions` claim per permission (`Role.PermissionsFor`), which FastEndpoints' `Permissions(...)` checks

  The claim names are `UseCases/Authorization/AppClaimTypes`.
- **Ending sessions:** Identity's `SecurityStampValidator` checks each cookie's security stamp every 30 minutes (its default `ValidationInterval`).
  - A password change or reset changes the stamp, so the other sessions end within 30 minutes.
  - `POST /auth/change-password` reissues this session's cookie (`RefreshSignInAsync`), so it carries on.
  - The validator rebuilds the claims each time, so a role change reaches a session within the same interval.
- **CSRF: antiforgery tokens.** The cookie now goes to every route.
  - `SameSite=Strict` keeps it off cross-site requests.
  - Cross-origin JSON needs a CORS preflight, which the API refuses.
  - But FastEndpoints binds form-encoded bodies too, and a page on a sibling subdomain is same-site. So a signed-in session's unsafe requests (anything but GET, HEAD, OPTIONS and TRACE) must carry an ASP.NET Core antiforgery token (`AntiforgeryConfigurations.UseSessionAntiforgery`, after authentication), or get a 400 problem.
  - `GET /auth/antiforgery-token` sets the token pair: the HttpOnly `apptemplate_antiforgery` cookie and the readable `XSRF-TOKEN` cookie. Angular's HttpClient sends `XSRF-TOKEN` back as `X-XSRF-TOKEN` by itself (`withXsrfConfiguration`).
  - Tokens are bound to the user, so the SPA fetches one at startup and again after signing in.
  - Anonymous requests aren't checked: they carry no credential to forge.
- **Not chosen:**
  - **FastEndpoints JWTs without refresh tokens:** a token in `localStorage` that XSS can read, and no way to end a session before it expires.
  - **`MapIdentityApi`:** its tokens are Identity's own opaque format and would sit in `localStorage` too, its endpoints are Minimal APIs rather than FastEndpoints, its `/register` can't create the profile (it takes no name), and its links point at the API, not the SPA.
  - **FastEndpoints' own antiforgery** (`EnableAntiforgery`, `UseAntiforgeryFE`): opt-in per endpoint and meant for form posts, so an endpoint that forgot it would be unprotected.

## Consequences
- **Much less code.** The aggregate, its table, the four use cases, the token service, the cookie and revocation classes, the JWT settings and signing key, `FastEndpoints.Security`, and the frontend's token store, refresh logic and lock are gone.
- **No token in JavaScript.** XSS can still act as the user while the page is open, but it can't steal a credential to use elsewhere.
- **Sign-out ends this browser's session only.** A copied cookie stays valid until it expires, or until the security stamp changes (a password change or reset). A "sign out everywhere" feature would call `UpdateSecurityStampAsync`.
- **Same site only.** The SPA and the API must be served from one site, and preferably one origin, as before. A cross-site deployment would need `SameSite=None`, CORS with credentials, and the antiforgery check would carry the whole CSRF defense.
- **Clients other than the browser** sign in with the same cookie: send `POST /auth/login`, keep the cookie, call `GET /auth/antiforgery-token`, and send its value as `X-XSRF-TOKEN`. Native or machine clients that want bearer tokens need another scheme, which is out of scope here.
- **Tests:** `TestSession` is a cookie jar that sends the token as Angular does. `SessionStampValidation` moves the clock past the validation interval, `CsrfProtection` covers the token rules, and `SessionCookie` covers the cookie and the 401/403 answers.
