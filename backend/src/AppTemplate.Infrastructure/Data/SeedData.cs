using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;
using AppTemplate.Infrastructure.Identity;
using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AppTemplate.Infrastructure.Data;

/// <summary>
/// Development-only demo data (see DatabaseConfigurations.StartDatabase). The accounts are created
/// confirmed, so they can sign in without an email round trip.
/// </summary>
public class SeedData
{
    public const int NUMBER_OF_SEED_USERS = 25;

    /// <summary>An account with the Admin role, for trying the users pages locally.</summary>
    public const string AdminEmail = "admin@example.com";
    public const string AdminPassword = "Admin123!";

    /// <summary>The shared password of the fixture users (user1@example.com, ...).</summary>
    public const string FixturePassword = "Passw0rd!";

    public static async Task InitializeAsync(
        ApplicationDatabaseContext dbContext,
        IIdentityService identity,
        CancellationToken cancellationToken = default
    )
    {
        if (await dbContext.UserProfiles.AnyAsync(cancellationToken))
            return; // DB has been seeded

        await PopulateTestDataAsync(identity, cancellationToken);
    }

    public static async Task PopulateTestDataAsync(
        IIdentityService identity,
        CancellationToken cancellationToken = default
    )
    {
        await CreateAsync(
            identity,
            "Admin",
            AdminEmail,
            AdminPassword,
            Role.Admin,
            cancellationToken
        );

        for (int i = 1; i <= NUMBER_OF_SEED_USERS; i++)
        {
            await CreateAsync(
                identity,
                $"User {i}",
                $"user{i}@example.com",
                FixturePassword,
                Role.Member,
                cancellationToken
            );
        }
    }

    private static async Task CreateAsync(
        IIdentityService identity,
        string name,
        string email,
        string password,
        Role role,
        CancellationToken cancellationToken
    )
    {
        var created = await identity.CreateUserAsync(
            UserName.From(name),
            new EmailAddress(email),
            password,
            role,
            emailConfirmed: true,
            cancellationToken
        );
        if (!created.IsSuccess)
        {
            throw new InvalidOperationException(
                $"Seeding {email} failed: {string.Join("; ", created.Errors)}"
            );
        }
    }
}
