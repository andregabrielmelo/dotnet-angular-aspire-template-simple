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
- **Permissions and roles are smart enums** ([Ardalis.SmartEnum](https://github.com/ardalis/SmartEnum)) in `UseCases/Authorization`:
  - **`Permission : SmartEnum<Permission, string>`:** `UsersRead`, `UsersWrite` and `UsersDelete`, whose `Value` is the claim (`users:read`, `users:write`, `users:delete`). A permission converts to its value implicitly, so it passes wherever a string is expected; in text, write `.Value` (its `ToString()` is the C# name).
  - **`Role : SmartEnum<Role>`:** `Admin` (every permission) and `Member` (none). A role carries its `Permissions`; its `Name` is the Identity role name and its `Value` the seeded role id.
  - **`Role.PermissionsFor(roleNames)`** maps an account's Identity roles to their permissions, without duplicates; an unknown role name grants nothing.
- **Accounts hold the roles** as ASP.NET Core Identity roles, seeded from `Role.List` by the migrations ([ADR 013]({{< relref "adr-013-user-management-aspnetcore-identity" >}})). Registration joins `Member`; the Development seed adds an `admin@example.com` admin.
- **The session carries them.** Identity's claims factory (`ApplicationClaimsPrincipalFactory`, Infrastructure) adds the roles (`role` claims) and one `permissions` claim per permission they grant, at sign-in and at every security stamp check ([ADR 015]({{< relref "adr-015-cookie-sessions-aspnetcore-identity" >}})). Web reads them back (`CurrentUser`, the `AppClaimTypes.Permission` claim type); there is no claims transformation.
- **Three levels of checks:**
  - **Endpoints** do the coarse checks with FastEndpoints' `Permissions(Permission.UsersRead)`, which checks the `permissions` claims. No policy per permission is registered.
  - **Handlers** decide what depends on the resource: `UpdateUserHandler` allows the caller's own profile (`ICurrentUser.Id == user.Id`) or anyone's with `users:write`, otherwise `Result.Forbidden()` → 403. Owning a profile is not a permission, which is why `Member` needs none.
  - **The domain** holds rules about the resource itself, when there are any.
- No authorization pipeline behavior. Add one when enough use cases share declarative requirements.
- **Clients may hint, the API decides.** `GET /users/me` returns the session's permissions so the SPA can hide actions (`permissionGuard`); the API enforces every one regardless.

## Consequences
- **Adding a protected operation** means a new `Permission` instance, adding it to the roles that grant it in `Role`, and `Permissions(...)` on the endpoint or an `ICurrentUser` check in the use case.
- **One place per role:** a role, what it grants and its seeded row all come from its `Role` instance. Before smart enums, they lived in three classes.
- A role change takes effect at the user's next security stamp check (within 30 minutes, Identity's default interval), not instantly.
- **A new role** is a `Role` instance and a fixed concurrency stamp in `IdentityRoleConfiguration`, then a migration (the new seeded row). Permissions per user would use Identity's user claims; the claims factory would add them.
- **Strings at the edges:** Identity stores role names, and tokens carry role names and permission values. The smart enums are the typed catalog the code uses in between.
- Changing someone's role is `UserManager.AddToRoleAsync` / `RemoveFromRoleAsync`, behind `IIdentityService` when a use case needs it.
- ADR 008's other consequences still hold: `EndpointAuthorization` (functional tests) pins the anonymous routes (now sign-in, registration and refresh), and resource-based rules stay in use cases. There is no authorization fallback policy: FastEndpoints endpoints are secure by default (see ADR 012 for the one route that isn't).
