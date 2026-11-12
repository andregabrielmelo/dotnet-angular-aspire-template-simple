---
title: "ADR 016: HTTPS in development with the ASP.NET Core development certificate"
weight: 160
---

# ADR 016: HTTPS in development with the ASP.NET Core development certificate

## Status
Accepted.

## Context
In development, the browser reached the Angular dev server over HTTP, and the dev server's proxy reached the API over HTTP. The API's `UseHttpsRedirection` then answered every proxied request with a redirect to its own HTTPS port. That is another origin, which the browser refused to follow (there is no CORS, and the CSP allows `connect-src 'self'` only), so every API call failed.

Avoiding the redirect would hide the mismatch. Instead, every request is HTTPS, development included, so development behaves like production: Secure cookies, the same redirect rules, and no "works over HTTP only" surprises.

Local HTTPS needs a certificate the browser trusts. There are two common conventions:
- **The ASP.NET Core development certificate** (`dotnet dev-certs https`, `aspire certs trust`). Kestrel, the Aspire dashboard and Aspire's `WithHttpsDeveloperCertificate()` all use it, and the .NET SDK already creates it.
- **mkcert**, a general-purpose tool that creates a local certificate authority. Angular and Vite guides often use it, but it's one more tool to install, next to the certificate .NET already has.

## Decision
- **One certificate: the ASP.NET Core development certificate.** Each developer trusts it once (`dotnet dev-certs https --trust`, or `aspire certs trust`). No certificate or key is ever generated into or committed to the repository.
- **The API listens on HTTPS only.** Its single launch profile is `https://localhost:7243`. The AppHost starts it with that profile (`launchProfileName: "https"`), and checks its health on the `https` endpoint.
- **The Angular dev server serves HTTPS.**
  - The AppHost declares `WithHttpsEndpoint(env: "PORT")` and `WithHttpsDeveloperCertificate()`.
  - `WithHttpsCertificateConfiguration` passes the certificate and key files to `ng serve` as `--ssl --ssl-cert … --ssl-key …`. The callback adds npm's `--` separator itself: Aspire adds one only for its own run-script arguments, and without it npm takes the `--ssl*` flags as its own options.
  - These APIs are experimental in Aspire 13 (`ASPIRECERTIFICATES001`).
- **The proxy verifies the API's certificate.**
  - It targets `services__web__https__0`, with `secure: true`.
  - `WithDeveloperCertificateTrust(true)` makes Node trust the development certificate.
- **Cookies are always `Secure`:** the session cookie and both antiforgery cookies, in every environment.
- **`UseHttpsRedirection` runs early, in every environment**, right after `UseForwardedHeaders`. In development nothing arrives over HTTP. Behind a host that terminates TLS, the forwarded scheme decides, so that host must be listed in `ForwardedHeaders:KnownProxies`. HSTS stays production only, because browsers cache it.
- **The AppHost has only its `https` profile**, so the dashboard and its OTLP and resource endpoints are HTTPS too.
- **Not covered:** Mailpit's web UI and Postgres stay as they are. They're development tools that carry no application traffic.

## Consequences
- **One-time setup:** run `dotnet dev-certs https --trust` (or `aspire certs trust`), then restart the browser.
  - **In WSL,** the command also adds the certificate to the Windows user's root store (with WSL interop on), so Edge and Firefox on Windows accept it.
  - **For `curl` and other OpenSSL tools in WSL or Linux,** set `SSL_CERT_DIR=$HOME/.aspnet/dev-certs/trust:/usr/lib/ssl/certs`.
- **The certificate expires after a year.** `dotnet dev-certs https --check --trust` shows its state, and `--clean` followed by `--trust` replaces it.
- **Running the frontend without the AppHost** needs the same flags: `npm start -- --ssl --ssl-cert <pem> --ssl-key <key>`, using files from `dotnet dev-certs https -ep <path>.pem --format Pem --no-password`. Keep those files out of the repository.
- **The OpenAPI export** (`scripts/export-openapi.sh`) still passes an HTTP URL: it writes the document and exits without serving a request, so CI needs no certificate.
- **Functional tests** already run on `https://localhost`, so Secure cookies behave as in production.
