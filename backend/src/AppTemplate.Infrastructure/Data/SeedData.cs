using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Infrastructure.Identity;
using AppTemplate.UseCases.Authorization;

namespace AppTemplate.Infrastructure.Data;

/// <summary>Development-only demo data (see DatabaseConfigurations.StartDatabase).</summary>
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
        IdentityService identity,
        CancellationToken cancellationToken = default
    )
    {
        if (await dbContext.UserProfiles.AnyAsync(cancellationToken))
            return; // DB has been seeded

        await PopulateTestDataAsync(identity, cancellationToken);
    }

    /// <summary>Accounts are created confirmed, so they can sign in without an email round trip.</summary>
    public static async Task PopulateTestDataAsync(
        IdentityService identity,
        CancellationToken cancellationToken = default
    )
    {
        await CreateAsync(
            identity,
            "Admin",
            AdminEmail,
            AdminPassword,
            Roles.Admin,
            cancellationToken
        );

        for (int i = 1; i <= NUMBER_OF_SEED_USERS; i++)
        {
            await CreateAsync(
                identity,
                $"User {i}",
                $"user{i}@example.com",
                FixturePassword,
                Roles.Member,
                cancellationToken
            );
        }
    }

    private static async Task CreateAsync(
        IdentityService identity,
        string name,
        string email,
        string password,
        string role,
        CancellationToken cancellationToken
    )
    {
        var created = await identity.CreateAccountAsync(
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
