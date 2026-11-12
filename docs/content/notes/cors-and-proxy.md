---
title: "CORS & the single origin"
weight: 50
---

# CORS & the single origin

The browser only ever talks to one origin: the Angular dev server in development (the `angular` resource's URL in the Aspire dashboard). Its proxy (`frontend/proxy.conf.mjs`) forwards `/api/**` to the Web API and strips the `/api` prefix. Aspire passes the API's HTTPS address in `services__web__https__0`. Both legs are HTTPS, with the ASP.NET Core development certificate, and the proxy verifies the API's certificate ([ADR 016]({{< relref "architecture-decisions/adr-016-https-in-development" >}})). Nothing is cross-origin, so no CORS configuration is needed, and the session and antiforgery cookies (`SameSite=Strict`, `Secure`) stay first-party ([ADR 015]({{< relref "architecture-decisions/adr-015-cookie-sessions-aspnetcore-identity" >}})).

Running `npm start` on its own targets the API's launch-profile port (`https://localhost:7243`). Pass the certificate to serve HTTPS: `npm start -- --ssl --ssl-cert <pem> --ssl-key <key>` (see ADR 016).

In production, keep the one origin: serve the built app and forward `/api` to the API from the same host, as described in [Production deployment]({{< relref "best-practices#production-deployment" >}}).
