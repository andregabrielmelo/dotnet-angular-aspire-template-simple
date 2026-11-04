using AppTemplate.Infrastructure.Identity;
using AppTemplate.IntegrationTests.Data;
using AppTemplate.UseCases.Authorization;
using Ardalis.Result;

namespace AppTemplate.IntegrationTests.Identity;

public class IdentityServiceCheckPassword(PostgresDatabaseFixture database)
    : BaseEfRepoTestFixture(database)
{
    private const string Password = "correct horse battery staple";

    private IdentityService Identity => Services.GetRequiredService<IdentityService>();

    private async Task<EmailAddress> AccountAsync(bool emailConfirmed)
    {
        var email = NewEmail();
        var created = await Identity.CreateAccountAsync(
            UserName.From("Ada"),
            email,
            Password,
            Roles.Admin,
            emailConfirmed,
            TestContext.Current.CancellationToken
        );
        created.IsSuccess.ShouldBeTrue();
        return email;
    }

    private Task<Result<UseCases.Authentication.AccountDto>> Check(
        EmailAddress email,
        string password
    ) => Identity.CheckPasswordAsync(email, password, TestContext.Current.CancellationToken);

    [Fact]
    public async Task ReturnsTheAccountWithItsRolesGivenTheRightPassword()
    {
        var email = await AccountAsync(emailConfirmed: true);

        var result = await Check(email, Password);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Roles.ShouldBe([Roles.Admin]);
        result.Value.SecurityStamp.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ReturnsUnauthorizedGivenAWrongPasswordOrAnUnknownEmail()
    {
        var email = await AccountAsync(emailConfirmed: true);

        (await Check(email, "a wrong password")).Status.ShouldBe(ResultStatus.Unauthorized);
        (await Check(NewEmail(), Password)).Status.ShouldBe(ResultStatus.Unauthorized);
    }

    [Fact]
    public async Task ReturnsForbiddenForAnUnconfirmedEmailOnlyGivenTheRightPassword()
    {
        var email = await AccountAsync(emailConfirmed: false);

        (await Check(email, "a wrong password")).Status.ShouldBe(ResultStatus.Unauthorized);
        (await Check(email, Password)).Status.ShouldBe(ResultStatus.Forbidden);
    }

    [Fact]
    public async Task LocksTheAccountOutAfterFiveFailures()
    {
        var email = await AccountAsync(emailConfirmed: true);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await Check(email, "a wrong password");
        }

        (await Check(email, Password)).Status.ShouldBe(ResultStatus.Unauthorized);
    }

    [Fact]
    public async Task CountsWrongPasswordsOnAnUnconfirmedAccountTowardLockout()
    {
        var email = await AccountAsync(emailConfirmed: false);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await Check(email, "a wrong password");
        }

        (await Check(email, Password)).Status.ShouldBe(ResultStatus.Unauthorized);
    }
}
