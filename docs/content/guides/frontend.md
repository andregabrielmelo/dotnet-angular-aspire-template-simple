---
title: "The frontend: configuration and API errors"
weight: 35
---

# The frontend: configuration and API errors

How the Angular app reaches the API without any per-environment configuration, and how it should show the errors the API returns. For the API's side of the error contract, see [ADR 009]({{< relref "architecture-decisions/adr-009-problem-details-error-contract" >}}).

## Reaching the API

There is no `environment.ts` and no API URL to configure. The app calls **relative URLs**, built from `API_BASE` (`frontend/src/app/core/api/api-paths.ts`, which is `'api'`), so every request goes to the origin that served the app:

| Where | Who serves the app | Who forwards `/api` |
|---|---|---|
| Development | `ng serve` (started by the AppHost) | the dev server's proxy, `frontend/proxy.conf.mjs`: strips `/api` and forwards to the API's HTTPS address, which Aspire passes as `services__web__https__0` |
| Production | your host or CDN | your reverse proxy, which strips `/api` the same way ([Deploying to production]({{< relref "deploying" >}})) |

Keep it that way:

- **Build feature URLs from `API_BASE`** (`` `${API_BASE}/users/${id}` ``), never from an absolute URL. A different API address belongs in the proxy's configuration, not in the app's code or build.
- **One origin is what makes the security work.** The session cookie is `SameSite=Strict` and HttpOnly, and the API has no CORS. The browser sends the cookie only because `/api` shares the app's origin, and the app never sees or stores a credential (nothing in `localStorage`).
- **The antiforgery token is automatic.** At startup and after signing in, `AuthService.fetchAntiforgeryToken()` calls `GET api/auth/antiforgery-token`, which sets the `XSRF-TOKEN` cookie. `withXsrfConfiguration` (in `app.config.ts`) makes `HttpClient` send it as `X-XSRF-TOKEN` on every unsafe request. Use `HttpClient` with relative URLs and you get it for free.
- **Types come from the API's contract.** `npm run api:generate` writes `src/app/core/api/api-types.ts` from `backend/openapi/v1.json`, so requests and responses are typed from what the API actually accepts ([API reference]({{< relref "api-reference" >}})).

## What errors look like

Every failure is an `HttpErrorResponse`. Its `status` tells you what happened, and its body (`error.error`) is one of these shapes:

| Status | When | Body |
|---|---|---|
| 400 | Request validation (FastEndpoints), or a use case's `Invalid` | `errors`: camelCase field name to messages, `{ "errors": { "name": ["Name is required"] } }` |
| 400 | A signed-in unsafe request without a valid antiforgery token | Problem details with `title` "Missing or invalid antiforgery token". Shouldn't happen when you use `HttpClient` as above. |
| 401 | Not signed in, the session expired, or wrong credentials at sign-in | Empty, or problem details from the auth endpoints |
| 403 | A missing permission, or a use case's `Forbidden` | Empty, or problem details with a `detail` |
| 404 | The resource doesn't exist | Problem details; `detail` holds the use case's message, if any |
| 409 | A conflict, such as a taken email | Problem details with the use case's `detail` |
| 429 | Rate limited | Problem details; the `Retry-After` header says when to retry |
| 500, 503 | Anything unexpected | A generic message; the details are only in the API's logs |

Both 400 shapes carry the same `errors` object, so a form shows field messages the same way whether the endpoint's validator or the use case rejected the input.

## Showing them

The pages follow one pattern. Keep to it in new features:

1. **Map the status to a message the user can act on**, specific to what they were doing: "You don't have permission to delete users." beats "Error 403". The auth pages share one helper, `describeAuthError` (`features/auth/auth-messages.ts`). Add one per feature when several pages need the same messages.
2. **On 400, show the field messages** from `error.error.errors`, next to the field they name (`errors['currentPassword']?.[0]`, `errors['name']?.[0]`), with a general fallback when the field isn't on the form.
3. **Never show the raw body or a 500's text.** The API keeps internal details out of responses; the UI shouldn't invent them either.
4. **Leave 401 alone.** `core/auth/auth-interceptor.ts` handles it everywhere outside `api/auth/*`: it forgets the user and sends them to `/auth/login?returnUrl=...`. Pages don't need their own 401 handling.
5. **Hide what the user can't do, but still handle 403.** `permissionGuard` and `CurrentUserService.hasPermission` hide pages and actions, but the API always decides, so a 403 can still arrive (for example after a role change, until the session's next security stamp check).

Examples to copy: `features/profile/profile-page.ts` (field errors on 400, a general message otherwise), `features/users/users-page.ts` (a specific 403 message), `features/auth/reset-password-page/reset-password-page.ts` (a 400 on `code` means an expired link).
