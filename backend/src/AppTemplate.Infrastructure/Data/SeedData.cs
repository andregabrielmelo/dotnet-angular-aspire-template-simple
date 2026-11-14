using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Infrastructure.Identity;
using AppTemplate.UseCases.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppTemplate.Infrastructure.Data;

/// <summary>
/// Development-only demo data (see DatabaseConfigurations.StartDatabase). The accounts are created
/// directly with Identity's <see cref="UserManager{TUser}"/>, already confirmed, so they can sign
/// in without an email round trip. It takes the scope's services, so the caller (Web) needs no
/// reference to ASP.NET Core Identity.
/// </summary>
public static class SeedData
{
    public const int NUMBER_OF_SEED_USERS = 25;

    /// <summary>An account with the Admin role, for trying the users pages locally.</summary>
    public const string AdminEmail = "admin@example.com";
    public const string AdminPassword = "Admin123!";

    /// <summary>The shared password of the fixture users (user1@example.com, ...).</summary>
    public const string FixturePassword = "Passw0rd!";

    /// <summary>Seeds the accounts, unless the database already has profiles.</summary>
    public static async Task InitializeAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default
    )
    {
        var dbContext = services.GetRequiredService<ApplicationDatabaseContext>();
        if (await dbContext.UserProfiles.AnyAsync(cancellationToken))
            return; // DB has been seeded

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        await CreateAsync(userManager, "Admin", AdminEmail, AdminPassword, Role.Admin);

        for (int i = 1; i <= NUMBER_OF_SEED_USERS; i++)
        {
            await CreateAsync(
                userManager,
                $"User {i}",
                $"user{i}@example.com",
                FixturePassword,
                Role.Member
            );
        }
    }

    /// <summary>
    /// Creates the profile with a confirmed account (one SaveChanges, through the account's
    /// <see cref="ApplicationUser.Profile"/>), then adds the account to <paramref name="role"/>.
    /// The email is also the user name, as in <c>IdentityService.CreateUserAsync</c>.
    /// </summary>
    private static async Task CreateAsync(
        UserManager<ApplicationUser> userManager,
        string name,
        string email,
        string password,
        Role role
    )
    {
        var profile = User.Create(UserName.From(name));
        var account = new ApplicationUser
        {
            ProfileId = profile.Id,
            Profile = profile,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };

        Succeeded(await userManager.CreateAsync(account, password), email);
        Succeeded(await userManager.AddToRoleAsync(account, role.Name), email);
    }

    private static void Succeeded(IdentityResult result, string email)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Seeding {email} failed: {string.Join("; ", result.Errors.Select(error => error.Description))}"
            );
        }
    }
}
