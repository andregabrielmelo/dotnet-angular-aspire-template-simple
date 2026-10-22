---
applyTo: "frontend/**"
---

# Frontend instructions

These extend [`AGENTS.md`](../../AGENTS.md). The general Angular guidelines are in [`docs/content/best-practices.md`](../../docs/content/best-practices.md#angular).

## Structure

- `core/`: app-wide singletons. Today this is the session (`core/auth/`): `CurrentUserService`, `authGuard`, `permissionGuard`, `authInterceptor` (adds `X-CSRF: 1`) and `BROWSER_REDIRECT`.
- `shared/`: UI and utilities that more than one feature genuinely reuses.
- `features/<feature>/`: everything a feature owns, meaning its pages, its `<feature>.service.ts` (HTTP), its `<feature>.model.ts` (API types) and its routes. Model new features on `features/users/`.
- File names are hyphenated, with one concept per file. A component's `.ts`, `.html`, `.css` and `.spec.ts` sit side by side.

## HTTP and authentication

- Components never inject `HttpClient` (ESLint enforces this outside `*service.ts`). API calls live in the feature's service, which returns `Observable`s and uses relative `api/...` URLs. The backend for frontend proxies them, so there is no base URL, no CORS and no tokens.
- API request and response types come from the OpenAPI document. Alias them in the feature's model file (`export type UserRow = ApiSchema<'UserRecord'>`) instead of writing interfaces by hand, and never edit `core/api/api-types.ts`; regenerate it with `npm run api:generate`.
- The browser never holds tokens. Session state comes from `CurrentUserService` and `/backend-for-frontend/user`. Login, register and logout are full-page redirects through `BROWSER_REDIRECT`.
- `authGuard` and `permissionGuard` only shape the UI. The API enforces every permission itself, so never rely on a guard for security.
- Never put secrets or environment-specific URLs in frontend code.

## Components and state

- Standalone components only. Use `inject()` in field initializers instead of constructor injection.
- Keep state local first, using `signal()`. When a few components share state, a service exposes it read-only through `computed()`. Don't add NgRx or another global store for trivial state.
- Use the native `@if`/`@for` control flow and `[class]`/`[style]` bindings instead of `NgClass`/`NgStyle`.

## Forms and errors

- Use reactive forms with explicit types. Client-side validation mirrors the API validator's rules for a better UX, but the server is the authority.
- Errors are RFC 9457 problem details. For a 400, `error.error.errors` maps camelCase field names (matching the form control names) to messages: show each next to its field. Show expected failures (403, 404, 409) as clear messages. Never show raw error bodies.

## Accessibility

- Use semantic HTML, a `<label>` for every input, buttons for actions and links for navigation, and keyboard operation. Associate error messages with their field (`aria-describedby`).
- `npm run lint` enforces Angular's template accessibility rules. Fix violations; don't disable them.

## Checks

From `frontend/`, run `npm run format:check`, `npm run lint`, `npm run build` and `npm run test` (Vitest, single run). Keep specs next to the file they test.
