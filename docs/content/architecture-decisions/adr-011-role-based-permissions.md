---
title: "ADR 011: Permissions granted by the user's role"
weight: 110
---

# ADR 011: Permissions granted by the user's role

## Status
Accepted. Supersedes [ADR 008]({{< relref "adr-008-permission-based-authorization" >}}), keeping its permissions, policies and resource-based checks.

## Context
ADR 008 kept fine-grained permissions in code and took them from Keycloak client roles in the access token. Without Keycloak ([ADR 010]({{< relref "adr-010-authentication-api-issued-tokens" >}})), something in the application has to say who holds which permission.

## Decision
- **Permissions in code, unchanged.** `users:read`, `users:write` and `users:delete`, defined once in `UseCases/Authorization/Permission.cs`.
- **Roles grant them.** The domain `User` has a `Role` (`Member` or `Admin`). `RolePermissions.For(role)` (UseCases) maps it: `Admin` has every permission, `Member` none. Registration creates members; the Development seed adds an `admin@example.com` admin.
- **The access token carries them.** `SessionFactory` writes the role's permissions into the token as `permission` claims when it issues one. Web reads them back (`HttpContextCurrentUser`, the `AppClaimTypes.Permission` claim type); there is no claims transformation.
- **Three levels of checks:**
  - **Endpoints** do the coarse checks: one ASP.NET Core policy per permission, with the same name, applied with `Policies(Permission.UsersRead)`.
  - **Handlers** decide what depends on the resource: `UpdateUserHandler` allows the caller's own profile (`ICurrentUser.Id == user.Id`) or anyone's with `users:write`, otherwise `Result.Forbidden()` → 403. Owning a profile is not a permission, which is why `Member` needs none.
  - **The domain** holds rules about the resource itself, when there are any.
- No authorization pipeline behavior. Add one when enough use cases share declarative requirements.
- **Clients may hint, the API decides.** `GET /users/me` returns the token's permissions so the SPA can hide actions (`permissionGuard`); the API enforces every one regardless.

## Consequences
- Adding a protected operation means a constant in `Permission`, a line in `RolePermissions`, and `Policies(...)` on the endpoint or an `ICurrentUser` check in the use case.
- A role change takes effect at the user's next refresh (within the 15-minute access token lifetime), not instantly.
- More roles, or permissions per user, mean extending `UserRole` and `RolePermissions` (or storing permissions on the user); the claims and policies stay as they are.
- ADR 008's other consequences still hold: the authorization fallback policy, `EndpointAuthorizationTests` pinning the anonymous routes (now including `/auth/*`), and resource-based rules in use cases.
