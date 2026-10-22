---
title: "ADR 008: Permission-based authorization with Keycloak client roles"
weight: 80
---

# ADR 008: Permission-based authorization with Keycloak client roles

## Status
Accepted

## Context
Until now any authenticated user could call every `/users` endpoint, including deleting other users. Only users with the right permissions may perform administrative actions. Some rules also depend on the resource itself: everyone may edit their own profile, but not anyone else's.

## Decision
- **Permissions, not roles, in code.**
  - The API knows fine-grained permissions (`users:read`, `users:write`, `users:delete`), defined once in `UseCases/Authorization/Permission.cs`.
  - In Keycloak they are **client roles on `apptemplate-api`**.
  - **Realm roles bundle them** (composite roles). The realm defines `admin`, which holds all three, and adding a role such as "support" (`users:read` only) is a Keycloak change, not a code change.
- **Token → permissions.** Keycloak puts client roles in the access token under `resource_access.apptemplate-api.roles`.
  - `KeycloakPermissionsClaimsTransformation` (Web) turns them into `permission` claims.
  - It maps only permissions the code knows, and a malformed claim grants nothing.
  - It adds a separate identity instead of mutating the token's one, and it is idempotent.
- **Coarse checks at the endpoint.** There is one ASP.NET Core policy per permission, with the same name. FastEndpoints endpoints opt in with `Policies(Permission.UsersRead)`, and the framework returns 403 without running the endpoint.
  - `GET /users` and `GET /users/{id}` require `users:read`.
  - `DELETE /users/{id}` requires `users:delete`.
- **Resource-based checks in the use case.**
  - `PUT /users/{id}` requires only authentication. `UpdateUserHandler` loads the user and allows the change if it is the caller's own profile (`User.ExternalId == sub`) or the caller has `users:write`. Otherwise it returns `Result.Forbidden()` → 403.
  - The caller is available through `ICurrentUser` (UseCases), implemented from the access token in Web.
- **Clients may hint, the API decides.** `GET /users/me` returns the caller's permissions so the SPA can hide actions (`permissionGuard`, conditional buttons). The API enforces every permission regardless of what the UI shows.
- **Development admin.** The realm imports `admin` / `admin` (email `admin@apptemplate.local`) with the `admin` role. The password is temporary, so Keycloak forces a change at first sign-in. New self-registered users get no API permissions.

## Consequences
- Adding a protected operation means adding a constant to `Permission`, a client role in the realm, adding it to the relevant composite roles, and calling `Policies(...)` on the endpoint or checking `ICurrentUser` in the use case.
- Permissions live in the access token, so a role change takes effect when the token is next refreshed (within the access-token lifespan, 5 minutes by default), not instantly.
- An authorization **fallback policy** requires an authenticated user on any route that declares neither `RequireAuthorization` nor `AllowAnonymous`. FastEndpoints 8.2 and 8.3 map `GET /_test_url_cache_` unconditionally and without metadata; it lists every route and endpoint type name, and was public until this policy. Operational routes that must stay open (health checks and the development-only Scalar page) call `AllowAnonymous()` explicitly.
- `EndpointAuthorizationTests` lists every anonymous route. Making an endpoint public means updating that list, which makes it a reviewed decision.
- Resource-based rules belong in use cases, where the resource is loaded and unit-testable. Don't put them in endpoints.
- The realm (roles, the admin user) is imported only while Keycloak's data volume is empty. Existing volumes need the roles added in the admin console, or the volume deleted.
