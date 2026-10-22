---
title: "ADR 007: Keycloak, a backend for frontend, and JWT Bearer for the API"
weight: 70
---

# ADR 007: Keycloak, a backend for frontend, and JWT Bearer for the API

## Status
Accepted

## Context
Users need to register, log in and log out. The earlier scaffolding kept access and refresh tokens in the browser's `localStorage`, where any injected script can read them, and grew a home-made token issuer on top of ASP.NET Core Identity that never compiled. What we want instead:

- **Login** through a standard OpenID Connect (OIDC) provider, with no custom credential or token code to maintain.
- A **secure, HTTP-only cookie** for the browser session, so JavaScript never touches a token.
- The **Web API** accepting only **OAuth 2.0 access tokens (JWTs)** as `Authorization: Bearer`, so other clients (mobile, services) can call it the same way.

## Decision

```
Browser ──(cookie)──► AppTemplate.BackendForFrontend ──(Bearer JWT)──► AppTemplate.Web
   └──(redirects)──► Keycloak (hosted login / registration / logout pages)
```

- **Keycloak** is the OIDC provider. It runs as an Aspire container (`Aspire.Hosting.Keycloak`), and the `apptemplate` realm is imported from `AppHost/Realms/apptemplate-realm.json`: self-registration is enabled, there is a confidential `apptemplate-backend-for-frontend` client (code flow + PKCE), and an audience mapper adds `apptemplate-api` to access tokens. Keycloak owns the credentials and hosts the login, registration and password pages.
- **`AppTemplate.BackendForFrontend`** is a small ASP.NET Core host and the browser's only origin:
  - It runs the OIDC authorization-code flow and issues the `__Host-apptemplate` cookie (`HttpOnly`, `Secure`, `SameSite=Strict`).
  - It exposes `/backend-for-frontend/login`, `/register` (sends `prompt=create`), `/user` and `/logout` for the SPA.
  - It proxies `/api/**` to the Web API with YARP, adding the user's access token as a Bearer header. Duende.AccessTokenManagement (Apache-2.0) refreshes that token with the refresh token.
  - In Development it also proxies everything else to the Angular dev server. Outside Development it serves the built SPA from `wwwroot`.
- **`AppTemplate.Web`** validates Keycloak JWTs (`AddKeycloakJwtBearer`, audience `apptemplate-api`). FastEndpoints endpoints require an authenticated user unless marked `AllowAnonymous()`.
- **Just-in-time provisioning**: registration happens in Keycloak, so the domain `User` is created on the first authenticated call to `GET /users/me`. It is linked to the Keycloak identity by `User.ExternalId`, which holds the `sub` claim.

## Consequences
- The browser never holds a token. The tokens are stored inside the encrypted cookie ticket, which the Data Protection keys protect and JavaScript cannot read. If the cookie grows too large, or tokens must be revocable server-side, add an `ITicketStore` backed by a distributed cache.
- **CSRF protection**:
  - `/api/**` and `/backend-for-frontend/user` require the header `X-CSRF: 1`. A cross-site page can't add a custom header without a CORS preflight, and this host allows no CORS.
  - Logout is a GET that must carry the session's own `sid`.
- **Fixed ports**: Keycloak listens on `8080` and the backend for frontend on `https://localhost:7100`, because the realm's redirect URIs and the token issuer must stay stable. If you change a port, update the realm file too.
- **Dev client secret**: `apptemplate-dev-secret-change-me` is committed in both the realm file and `AppHost/appsettings.Development.json`. It is for local development only. Give each environment its own secret and pass it as the `keycloak-backend-for-frontend-secret` Aspire parameter.
- **Tests**: functional tests replace JWT validation with a `TestAuthHandler` (`X-Test-User` header). `AppTemplate.BackendForFrontend.Tests` hosts the backend for frontend with a static OpenID Connect discovery document to check the login redirects (PKCE, `prompt=create`), the 401 responses and the CSRF header.
- **Authority outside development**: the Aspire service-discovery address (`https+http://keycloak/...`) fails OpenID Connect's HTTPS check, so set `Keycloak:Authority` to the realm's public HTTPS URL on both the backend for frontend and the Web API.
- `Aspire.Hosting.Keycloak` and `Aspire.Keycloak.Authentication` are still preview packages.
