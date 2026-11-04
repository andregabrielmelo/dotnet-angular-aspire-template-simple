---
applyTo: "frontend/**"
---

# Frontend instructions

These extend [`AGENTS.md`](../../AGENTS.md). The general Angular guidelines are in [`docs/content/best-practices.md`](../../docs/content/best-practices.md#angular). For a complete feature (model, service, page, route and specs), see step 8 of [Adding a feature, step by step](../../docs/content/guides/adding-a-feature.md#8-frontend-the-angular-feature).

## Structure

- `core/`: app-wide singletons. Today this is the session (`core/auth/`): `CurrentUserService`, `authGuard`, `permissionGuard`, `authInterceptor` (adds the access token, refreshes it before it expires and once on a 401) and `REFRESH_LOCK`. The session itself is `AuthService` (`features/auth/`).
- `shared/`: UI and utilities that more than one feature genuinely reuses.
- `features/<feature>/`: everything a feature owns, meaning its pages, its `<feature>.service.ts` (HTTP), its `<feature>.model.ts` (API types) and its routes. Model new features on `features/users/`.
- File names are hyphenated, with one concept per file. A component's `.ts`, `.html`, `.css` and `.spec.ts` sit side by side.

## HTTP and authentication

- Components never inject `HttpClient` (ESLint enforces this outside `*service.ts`). API calls live in the feature's service, which returns `Observable`s and uses relative `api/...` URLs. The dev server proxies them to the API (`proxy.conf.mjs`), so there is no base URL and no CORS.
- API request and response types come from the OpenAPI document. Alias them in the feature's model file (`export type UserRow = ApiSchema<'UserRecord'>`) instead of writing interfaces by hand, and never edit `core/api/api-types.ts`; regenerate it with `npm run api:generate`.
- `AuthService` owns the session: the access token and its expiry in `localStorage` (`apptemplate.session`), shared by tabs. Never read or write that key elsewhere, and never store a token anywhere else. The refresh token is an HttpOnly cookie the app can't read, sent by the browser only to `api/auth/refresh-token`; `ensureSession()` and `refresh()` use it. Profile and permissions come from `CurrentUserService`. Never add the token yourself: `authInterceptor` does (`AuthService` adds it itself to the `api/auth/*` calls that need it, which the interceptor skips).
- Account flows that don't change the session (confirm email, resend, forgot and reset password) live in `features/auth/account.service.ts`; their pages read the emailed link's query parameters as component inputs (`withComponentInputBinding`).
- `authGuard` and `permissionGuard` only shape the UI. The API enforces every permission itself, so never rely on a guard for security.
- Never put secrets or environment-specific URLs in frontend code.

## Components and state

- Standalone components only. Use `inject()` in field initializers instead of constructor injection.
- Keep state local first, using `signal()`. When a few components share state, a service exposes it read-only through `computed()`. Don't add NgRx or another global store for trivial state.
- Use the native `@if`/`@for` control flow and `[class]`/`[style]` bindings instead of `NgClass`/`NgStyle`.

## Forms and errors

- Use reactive forms with explicit types. Client-side validation mirrors the API validator's rules for a better UX, but the server is the authority.
- For a 400, `error.error.errors` maps camelCase field names (matching the form control names) to messages, both for request validation and for a use case's `Invalid` result: show each next to its field. Show expected failures (403, 404, 409) as clear messages. Never show raw error bodies.

## Accessibility

- Use semantic HTML, a `<label>` for every input, buttons for actions and links for navigation, and keyboard operation. Associate error messages with their field (`aria-describedby`).
- `npm run lint` enforces Angular's template accessibility rules. Fix violations; don't disable them.

## Checks

From `frontend/`, run `npm run format:check`, `npm run lint`, `npm run build` and `npm run test` (Vitest, single run). Keep specs next to the file they test.
