---
title: "ADR 013: User management with ASP.NET Core Identity"
weight: 130
---

# ADR 013: User management with ASP.NET Core Identity

## Status
Accepted. Amends [ADR 012]({{< relref "adr-012-jwt-authentication-fastendpoints" >}}): accounts move to ASP.NET Core Identity, while its JWTs, refresh token cookie and revocation stay as they are. Changes [ADR 011]({{< relref "adr-011-role-based-permissions" >}})'s roles into Identity roles; ADR 014 adds rate limits to the account endpoints. Amended by [ADR 015]({{< relref "adr-015-cookie-sessions-aspnetcore-identity" >}}): the JWTs and refresh tokens are gone, and Identity's own cookie is the session, so the parts below about Identity Core without cookies and about ending sessions at refresh describe the earlier setup.

## Context
ADR 012 left the API with only the minimum: one hashed password per user, checked by hand. It had no lockout, no email confirmation, no password reset and no way to end sessions after a password change, and ADR 012 listed those as gaps to fill before going to production. Writing them by hand is where account security usually goes wrong: reset tokens that don't expire or can be reused, lockouts that reveal who has an account, password changes that leave old sessions alive.

ASP.NET Core Identity provides all of them, maintained and tested. The [FastEndpoints security docs](https://fast-endpoints.com/docs/security) and their [Jwt-And-Identity-Auth-Demo](https://github.com/FastEndpoints/Jwt-And-Identity-Auth-Demo) show Identity used together with FastEndpoints' JWTs.

## Decision
- **Identity Core, without its cookies.**
  - `AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole<int>>().AddEntityFrameworkStores().AddSignInManager().AddDefaultTokenProviders()` (Infrastructure).
  - The demo uses `AddDefaultIdentity`, which registers Identity's cookie schemes and then makes JWT the default. Identity Core registers no scheme at all, so FastEndpoints' JWT stays the only way to authenticate. (ADR 015 later switches to `AddIdentity` and its cookie.)
  - `SignInManager` is used only for `CheckPasswordSignInAsync`, the password check with lockout; it never signs anyone in.
  - `MapIdentityApi` isn't used: its endpoints are Minimal APIs, its tokens are opaque rather than JWTs, and it returns the refresh token in the body. Every endpoint stays a FastEndpoints class.
- **An account and a profile.** Core may not reference `Microsoft.AspNetCore.Identity` (ADR 001, `LayerDependencyTests`), so a person is two things:
  - **The profile** is the domain `User` aggregate (name, phone number), whose `UserId` is what the application uses, including the session's `user_id` claim (ADR 015).
  - **The account** is `ApplicationUser : IdentityUser<int>` in Infrastructure: email, password hash, lockout, security stamp and roles.
  - **The link:** the account's `ProfileId` is a foreign key to the profile, unique and cascading, so deleting a user through the repository deletes the account and Identity's rows with it.
  - **Why the account points at the profile:** EF Core can't relate the Vogen `UserId` to Identity's `int` key the other way round.
  - **Creation:** `IdentityService` creates both in one transaction.
  - This is jasontaylordev/CleanArchitecture's `ApplicationUser` plus `IIdentityService`, keeping the domain aggregate as ardalis/CleanArchitecture does.
- **One port.** UseCases reach Identity only through `IIdentityService` (`UseCases/Interfaces`): create an account, check a password, read an account, send and use confirmation and reset codes, and change a password. No Identity type appears in its signatures.
  - `IdentityService` implements it over `UserManager` and `SignInManager`.
  - `LayerDependencyTests` checks that Web doesn't depend on `Microsoft.AspNetCore.Identity`.
- **Tables:** `ApplicationDatabaseContext : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>`, one database and one transaction. The `asp_net_*` tables are renamed to snake_case like every other table.
- **Roles:** Identity roles named and numbered by the `Role` smart enum ([ADR 011]({{< relref "adr-011-role-based-permissions" >}})). `IdentityRoleConfiguration` seeds `Role.List` through the migrations, so they exist in every environment. Registration joins `Member`. `Role.PermissionsFor` maps role names to permissions, and tokens carry both `role` and `permissions` claims.
- **Passwords:** length only, at least 8 characters, no composition rules (NIST SP 800-63B), enforced by Identity; validators answer sooner with the same limits (`PasswordConstants`). The email is the user name, so the unique `normalized_user_name` index stops two concurrent registrations with one address.
- **Lockout:** 5 failed attempts lock the account for 5 minutes.
  - A wrong password, an unknown email and a locked-out account get the same 401, and an unknown email still costs a hash.
  - Only the right password on an unconfirmed email gets 403. Identity checks confirmation before the password, so `IdentityService` checks the password itself in that case, and counts a wrong one toward lockout.
- **Email confirmation is required** (`SignIn.RequireConfirmedEmail`).
  - Registration returns 201 without tokens. `UserRegisteredDomainEvent`'s handler (`SendConfirmationOnUserRegistered`, UseCases) emails a confirmation link, which doubles as the welcome email.
  - `POST /auth/confirm-email` takes its `userId` and `code`; `POST /auth/resend-confirmation` sends another, always answering 202.
- **Password reset:** `POST /auth/forgot-password` emails a reset link to a confirmed account, always answering 202. `POST /auth/reset-password` sets the new password and lifts a lockout. Reset and confirmation links both use Identity's default token provider, so they expire after a day.
- **Links:**
  - Codes are Identity's, protected with Data Protection and Base64Url-encoded as Identity's own endpoints do.
  - **The key ring lives in Postgres** (`PersistKeysToDbContext`, the `data_protection_keys` table, application name `AppTemplate`), so a link stays valid after a restart and on every instance.
  - They are emailed by `AccountEmailSender`, Identity's `IEmailSender<TUser>` over the application's MailKit sender.
  - `AccountLinks:ClientUrl` (required) is the web app the links open; the AppHost sets it to the Angular app.
- **Ending sessions:** a password change or reset changes the account's security stamp, and Identity's `SecurityStampValidator` ends every other session within 30 minutes (its default interval). `POST /auth/change-password` reissues the cookie of the device that changed it (ADR 015).

## Consequences
- **Built in, not hand-written:** lockout, confirmation, reset and stamp-based session ending come from Identity, and the template only maps them to endpoints and results.
- **Two user types.** The profile and the account are read separately: `GetCurrentUser` takes the email from the account. A new account field (such as a confirmed phone number) goes on `ApplicationUser`, a new profile field on `User`.
- **Stale access tokens:** after a password change, other devices' access tokens keep working until they expire (at most 15 minutes), like a revoked role; only their refresh fails.
- **Data Protection keys** protect the confirmation and reset codes. By default they are kept on the machine, so a restart of a container without persistent keys, or a second instance, invalidates links already sent. Persist the key ring and share it between instances in production (best-practices.md, Production deployment).
- **Email enumeration and abuse:** forgot-password and resend-confirmation never reveal whether an account exists, but they send email anonymously. With no rate limiting in the template, add one to them (and to login and registration) before going public; see the cookbook's "Limit one endpoint".
- **Roles in the migrations:** new roles go into `ApplicationRoleConfiguration` with the next id, because Postgres doesn't advance an identity column for seeded ids.
- **Not included:** two-factor authentication, external sign-in and email changes. Identity supports them (`UserManager`'s authenticator keys, `ExternalLoginSignInAsync`, `ChangeEmailAsync`), so each is an endpoint and a port method away.
- **Tests:**
  - The integration tests cover account creation (rollback, conflict, a concurrent race), lockout, codes and the cascade on a real Postgres, and that a code made by one service provider is accepted by another (`AccountCodeProtection`). Identity's token provider reads the system clock, so the one-day code lifespan is checked as configuration, not by moving time.
  - The functional tests drive confirmation, reset and change password through the real endpoints. They lower PBKDF2's iteration count to stay fast.
