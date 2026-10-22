---
title: "CORS & the single origin"
weight: 50
---

# CORS & the single origin

The browser only ever talks to one origin: `AppTemplate.BackendForFrontend` (`https://localhost:7100` in development). That host proxies `/api/**` to the Web API, handles the `/backend-for-frontend/**` session endpoints itself, and in development proxies everything else to the Angular dev server. Nothing is cross-origin, so no CORS configuration is needed. This also keeps the session cookie, the OpenID Connect redirect URIs and `SameSite=Strict` all on the same origin. See [ADR 007]({{< relref "architecture-decisions/adr-007-authentication-backend-for-frontend-keycloak" >}}).

The Angular dev server no longer has a `proxy.conf.ts`: open the app through the backend for frontend, not the dev server's own port.
