---
title: "ADR 011: Permissions granted by the user's role"
weight: 110
---

# ADR 011: Permissions granted by the user's role

## Status
Accepted. Supersedes [ADR 008]({{< relref "adr-008-permission-based-authorization" >}}), keeping its permissions and resource-based checks. Endpoints check permissions with FastEndpoints' `Permissions()` rather than one policy per permission.

## Context
ADR 008 kept fine-grained permissions in code and took them from Keycloak client roles in the access token. Without Keycloak ([ADR 010]({{< relref "adr-010-authentication-api-issued-tokens" >}}), then [ADR 012]({{< relref "adr-012-jwt-authentication-fastendpoints" >}})), something in the application has to say who holds which permission.

## Decision
- **Permissions in code, unchanged.** `users:read`, `users:write` and `users:delete`, defined once in `UseCases/Authorization/Permission.cs`.
- **Roles grant them.** The domain `User` has a `Role` (`Member` or `Admin`). `RolePermissions.For(role)` (UseCases) maps it: `Admin` has every permission, `Member` none. Registration creates members; the Development seed adds an `admin@example.com` admin.
- **The access token carries them.** `UserPrivilegesExtensions.AddUser` (Web) adds the role's permissions to FastEndpoints' `UserPrivileges`, at sign-in and at every refresh, so they become `permissions` claims. Web reads them back (`HttpContextCurrentUser`, the `AppClaimTypes.Permission` claim type); there is no claims transformation.
- **Three levels of checks:**
  - **Endpoints** do the coarse checks with FastEndpoints' `Permissions(Permission.UsersRead)`, which checks the `permissions` claims. No policy per permission is registered.
  - **Handlers** decide what depends on the resource: `UpdateUserHandler` allows the caller's own profile (`ICurrentUser.Id == user.Id`) or anyone's with `users:write`, otherwise `Result.Forbidden()` → 403. Owning a profile is not a permission, which is why `Member` needs none.
  - **The domain** holds rules about the resource itself, when there are any.
- No authorization pipeline behavior. Add one when enough use cases share declarative requirements.
- **Clients may hint, the API decides.** `GET /users/me` returns the token's permissions so the SPA can hide actions (`permissionGuard`); the API enforces every one regardless.

## Consequences
- Adding a protected operation means a constant in `Permission`, a line in `RolePermissions`, and `Permissions(...)` on the endpoint or an `ICurrentUser` check in the use case.
- A role change takes effect at the user's next refresh (within the 15-minute access token lifetime), not instantly.
- More roles, or permissions per user, mean extending `UserRole` and `RolePermissions` (or storing permissions on the user); the claims and policies stay as they are.
- ADR 008's other consequences still hold: `EndpointAuthorization` (functional tests) pins the anonymous routes (now login, register and `POST /auth/refresh-token`), and resource-based rules stay in use cases. The authorization fallback policy remains for the reason in ADR 012.
