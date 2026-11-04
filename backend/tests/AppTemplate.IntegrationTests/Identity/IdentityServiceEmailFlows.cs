using AppTemplate.Infrastructure.Identity;
using AppTemplate.IntegrationTests.Data;
using AppTemplate.UseCases.Authorization;
using Ardalis.Result;
using Microsoft.AspNetCore.WebUtilities;

namespace AppTemplate.IntegrationTests.Identity;

public class IdentityServiceEmailFlows(PostgresDatabaseFixture database)
    : BaseEfRepoTestFixture(database)
{
    private const string Password = "correct horse battery staple";
    private const string NewPassword = "an entirely new passphrase";

    private IdentityService Identity => Services.GetRequiredService<IdentityService>();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private async Task<(UserId Id, EmailAddress Email)> AccountAsync(bool emailConfirmed)
    {
        var email = NewEmail();
        var created = await Identity.CreateAccountAsync(
            UserName.From("Ada"),
            email,
            Password,
            Roles.Member,
            emailConfirmed,
            Cancellation
        );
        return (created.Value, email);
    }

    private Dictionary<string, string> LastLinkQuery(EmailAddress email) =>
        QueryHelpers
            .ParseQuery(Emails.LastLinkTo(email.Value).Query)
            .ToDictionary(part => part.Key, part => part.Value.ToString());

    [Fact]
    public async Task ConfirmsTheEmailWithTheCodeFromTheLink()
    {
        var (id, email) = await AccountAsync(emailConfirmed: false);

        await Identity.SendEmailConfirmationAsync(id, Cancellation);
        var link = Emails.LastLinkTo(email.Value);
        var query = LastLinkQuery(email);

        link.AbsolutePath.ShouldBe("/auth/confirm-email");
        query["userId"].ShouldBe(id.Value.ToString());
        (
            await Identity.ConfirmEmailAsync(id, query["code"], Cancellation)
        ).IsSuccess.ShouldBeTrue();
        (await Identity.GetAccountAsync(id, Cancellation))!.EmailConfirmed.ShouldBeTrue();
    }

    [Fact]
    public async Task RejectsAWrongConfirmationCode()
    {
        var (id, _) = await AccountAsync(emailConfirmed: false);

        var result = await Identity.ConfirmEmailAsync(id, "bm90LWEtY29kZQ", Cancellation);

        result.Status.ShouldBe(ResultStatus.Invalid);
        result.ValidationErrors.Single().Identifier.ShouldBe("code");
    }

    [Fact]
    public async Task SendsNoConfirmationToAnAccountAlreadyConfirmed()
    {
        var (id, email) = await AccountAsync(emailConfirmed: true);

        await Identity.SendEmailConfirmationAsync(id, Cancellation);

        Emails.Sent.ShouldNotContain(sent => sent.To == email.Value);
    }

    [Fact]
    public async Task ResetsThePasswordWithTheCodeFromTheLinkAndChangesTheStamp()
    {
        var (id, email) = await AccountAsync(emailConfirmed: true);
        var stampBefore = (await Identity.GetAccountAsync(id, Cancellation))!.SecurityStamp;

        await Identity.SendPasswordResetAsync(email, Cancellation);
        var query = LastLinkQuery(email);
        var reset = await Identity.ResetPasswordAsync(
            email,
            query["code"],
            NewPassword,
            Cancellation
        );

        reset.IsSuccess.ShouldBeTrue();
        query["email"].ShouldBe(email.Value);
        (await Identity.GetAccountAsync(id, Cancellation))!.SecurityStamp.ShouldNotBe(stampBefore);
        (
            await Identity.CheckPasswordAsync(email, NewPassword, Cancellation)
        ).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task SendsNoResetLinkToAnUnknownOrUnconfirmedEmail()
    {
        var (_, unconfirmed) = await AccountAsync(emailConfirmed: false);
        var unknown = NewEmail();

        await Identity.SendPasswordResetAsync(unconfirmed, Cancellation);
        await Identity.SendPasswordResetAsync(unknown, Cancellation);

        Emails.Sent.ShouldNotContain(sent =>
            sent.To == unconfirmed.Value || sent.To == unknown.Value
        );
    }

    [Fact]
    public async Task ChangesThePasswordOnlyGivenTheCurrentOne()
    {
        var (id, email) = await AccountAsync(emailConfirmed: true);
        var stampBefore = (await Identity.GetAccountAsync(id, Cancellation))!.SecurityStamp;

        var wrong = await Identity.ChangePasswordAsync(
            id,
            "not my password",
            NewPassword,
            Cancellation
        );
        var changed = await Identity.ChangePasswordAsync(id, Password, NewPassword, Cancellation);

        wrong.Status.ShouldBe(ResultStatus.Invalid);
        wrong.ValidationErrors.Single().Identifier.ShouldBe("currentPassword");
        changed.IsSuccess.ShouldBeTrue();
        (await Identity.GetAccountAsync(id, Cancellation))!.SecurityStamp.ShouldNotBe(stampBefore);
        (
            await Identity.CheckPasswordAsync(email, NewPassword, Cancellation)
        ).IsSuccess.ShouldBeTrue();
    }
}
