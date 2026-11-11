using AppTemplate.Infrastructure.Identity;
using AppTemplate.IntegrationTests.Data;
using AppTemplate.UseCases.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace AppTemplate.IntegrationTests.Identity;

/// <summary>
/// How confirmation and reset codes are protected: the Data Protection keys live in Postgres, so a
/// code stays valid for another instance or after a restart, and reset codes have a short life.
/// </summary>
public class AccountCodeProtection(PostgresDatabaseFixture database)
    : BaseEfRepoTestFixture(database)
{
    [Fact]
    public async Task KeepsTheKeyRingInPostgres()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var email = NewEmail();
        var created = await Services
            .GetRequiredService<IdentityService>()
            .CreateUserAsync(
                UserName.From("Ada"),
                email,
                "correct horse battery staple",
                Role.Member,
                emailConfirmed: false,
                cancellationToken
            );

        await Services
            .GetRequiredService<IdentityService>()
            .SendEmailConfirmationAsync(created.Value, cancellationToken);

        (await DbContext.DataProtectionKeys.CountAsync(cancellationToken)).ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task AcceptsACodeMadeByAnotherServiceProviderOnTheSameDatabase()
    {
        // Another instance: its own services, the same database and so the same key ring.
        var cancellationToken = TestContext.Current.CancellationToken;
        var email = NewEmail();
        var identity = Services.GetRequiredService<IdentityService>();
        var created = await identity.CreateUserAsync(
            UserName.From("Ada"),
            email,
            "correct horse battery staple",
            Role.Member,
            emailConfirmed: false,
            cancellationToken
        );
        await identity.SendEmailConfirmationAsync(created.Value, cancellationToken);
        var code = QueryHelpers.ParseQuery(Emails.LastLinkTo(email.Value).Query)["code"].ToString();

        await using var otherInstance = await PostgresDatabaseFixture.ServicesForAsync(
            DbContext.Database.GetConnectionString()!,
            Emails
        );
        using var scope = otherInstance.CreateScope();
        var confirmed = await scope
            .ServiceProvider.GetRequiredService<IdentityService>()
            .ConfirmEmailAsync(created.Value, code, cancellationToken);

        confirmed.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void GivesConfirmationAndResetCodesIdentitysDefaultDay()
    {
        var identity = Services.GetRequiredService<IOptions<IdentityOptions>>().Value;
        var codes = Services
            .GetRequiredService<IOptions<DataProtectionTokenProviderOptions>>()
            .Value;

        identity.Tokens.PasswordResetTokenProvider.ShouldBe(TokenOptions.DefaultProvider);
        identity.Tokens.EmailConfirmationTokenProvider.ShouldBe(TokenOptions.DefaultProvider);
        codes.TokenLifespan.ShouldBe(TimeSpan.FromDays(1));
    }
}
