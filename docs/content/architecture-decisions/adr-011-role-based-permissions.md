---
title: "ADR 011: Permissions granted by the user's role"
weight: 110
---

# ADR 011: Permissions granted by the user's role

## Status
Accepted. Supersedes [ADR 008]({{< relref "adr-008-permission-based-authorization" >}}), keeping its permissions and resource-based checks. Endpoints check permissions with FastEndpoints' `Permissions()` rather than one policy per permission, and the roles are ASP.NET Core Identity roles ([ADR 013]({{< relref "adr-013-user-management-aspnetcore-identity" >}})).

## Context
ADR 008 kept fine-grained permissions in code and took them from Keycloak client roles in the access token. Without Keycloak ([ADR 010]({{< relref "adr-010-authentication-api-issued-tokens" >}}), then [ADR 012]({{< relref "adr-012-jwt-authentication-fastendpoints" >}})), something in the application has to say who holds which permission.

## Decision
- **Permissions in code, unchanged.** `users:read`, `users:write` and `users:delete`, defined once in `UseCases/Authorization/Permission.cs`.
- **Roles grant them.** An account has Identity roles, `Admin` or `Member` (`UseCases/Authorization/Roles`), seeded by the migrations. `RolePermissions.For(roles)` (UseCases) maps them: `Admin` has every permission, `Member` none, and an unknown role grants nothing. Registration joins `Member`; the Development seed adds an `admin@example.com` admin.
- **The access token carries them.** `UserPrivilegesExtensions.AddUser` (Web) adds the roles (`role` claims) and their permissions to FastEndpoints' `UserPrivileges`, at sign-in and at every refresh, so they become `permissions` claims. Web reads them back (`HttpContextCurrentUser`, the `AppClaimTypes.Permission` claim type); there is no claims transformation.
- **Three levels of checks:**
  - **Endpoints** do the coarse checks with FastEndpoints' `Permissions(Permission.UsersRead)`, which checks the `permissions` claims. No policy per permission is registered.
  - **Handlers** decide what depends on the resource: `UpdateUserHandler` allows the caller's own profile (`ICurrentUser.Id == user.Id`) or anyone's with `users:write`, otherwise `Result.Forbidden()` → 403. Owning a profile is not a permission, which is why `Member` needs none.
  - **The domain** holds rules about the resource itself, when there are any.
- No authorization pipeline behavior. Add one when enough use cases share declarative requirements.
- **Clients may hint, the API decides.** `GET /users/me` returns the token's permissions so the SPA can hide actions (`permissionGuard`); the API enforces every one regardless.

## Consequences
- Adding a protected operation means a constant in `Permission`, a line in `RolePermissions`, and `Permissions(...)` on the endpoint or an `ICurrentUser` check in the use case.
- A role change takes effect at the user's next refresh (within the 15-minute access token lifetime), not instantly.
- A new role means a constant in `Roles`, a seeded row in `ApplicationRoleConfiguration` (and a migration), and a line in `RolePermissions`. Permissions per user would use Identity's user claims; the token's claims stay as they are.
- Changing someone's role is `UserManager.AddToRoleAsync` / `RemoveFromRoleAsync`, behind `IIdentityService` when a use case needs it.
- ADR 008's other consequences still hold: `EndpointAuthorization` (functional tests) pins the anonymous routes (now sign-in, registration, refresh and the account email endpoints), and resource-based rules stay in use cases. The authorization fallback policy remains for the reason in ADR 012.
