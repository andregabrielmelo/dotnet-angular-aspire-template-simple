---
title: "CORS & the single origin"
weight: 50
---

# CORS & the single origin

The browser only ever talks to one origin: the Angular dev server in development (the `angular` resource's URL in the Aspire dashboard). Its proxy (`frontend/proxy.conf.mjs`) forwards `/api/**` to the Web API, strips the `/api` prefix and adds `X-Forwarded-For`; Aspire passes the API's address in `services__web__http__0`. Nothing is cross-origin, so no CORS configuration is needed, and the refresh token cookie (`SameSite=Strict`, path `/api/auth`) stays first-party. See [ADR 010]({{< relref "architecture-decisions/adr-010-authentication-api-issued-tokens" >}}).

Refresh and sign-out check the request's `Origin` against `Authentication:AllowedOrigins`, which the AppHost sets to the Angular endpoint. Running `npm start` on its own targets the API's launch-profile port (`http://localhost:5234`) and origin `http://localhost:4200`.

In production, keep the one origin: serve the built app and forward `/api` to the API from the same host, as described in [Production deployment]({{< relref "best-practices#production-deployment" >}}).
