using AppTemplate.Infrastructure.Identity;
using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace AppTemplate.IntegrationTests.Data;

/// <summary>
/// <see cref="SeedData"/> creates the development accounts through UserManager: a confirmed admin
/// who can sign in, the fixture members, and nothing more on a second run.
/// </summary>
public class SeedDataInitialize(PostgresDatabaseFixture database) : BaseEfRepoTestFixture(database)
{
    [Fact]
    public async Task SeedsAConfirmedAdminAndTheMembersOnce()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var userManager = Services.GetRequiredService<UserManager<ApplicationUser>>();

        await SeedData.InitializeAsync(Services, cancellationToken);
        await SeedData.InitializeAsync(Services, cancellationToken);

        var admin = await userManager.FindByEmailAsync(SeedData.AdminEmail);
        admin.ShouldNotBeNull();
        admin.EmailConfirmed.ShouldBeTrue();
        (await userManager.GetRolesAsync(admin)).ShouldBe([Role.Admin.Name]);
        (
            await Services
                .GetRequiredService<IIdentityService>()
                .CheckPasswordAsync(
                    new EmailAddress(SeedData.AdminEmail),
                    SeedData.AdminPassword,
                    cancellationToken
                )
        ).IsSuccess.ShouldBeTrue();

        (await userManager.GetUsersInRoleAsync(Role.Member.Name)).Count.ShouldBe(
            SeedData.NUMBER_OF_SEED_USERS
        );
        (await DbContext.UserProfiles.CountAsync(cancellationToken)).ShouldBe(
            SeedData.NUMBER_OF_SEED_USERS + 1
        );
    }
}
