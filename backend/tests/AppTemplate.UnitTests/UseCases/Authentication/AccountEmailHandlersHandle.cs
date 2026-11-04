using AppTemplate.UseCases.Authentication;
using AppTemplate.UseCases.Authentication.ConfirmEmail;
using AppTemplate.UseCases.Authentication.ForgotPassword;
using AppTemplate.UseCases.Authentication.ResendConfirmation;
using AppTemplate.UseCases.Authentication.ResetPassword;

namespace AppTemplate.UnitTests.UseCases.Authentication;

/// <summary>The thin handlers over <see cref="IIdentityService"/>'s email and code flows.</summary>
public class AccountEmailHandlersHandle
{
    private readonly IIdentityService _identity = Substitute.For<IIdentityService>();
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ConfirmEmailPassesOnTheIdentityResult()
    {
        _identity
            .ConfirmEmailAsync(UserId.From(3), "code", Arg.Any<CancellationToken>())
            .Returns(
                Result.Invalid(new ValidationError { Identifier = "code", ErrorMessage = "x" })
            );

        var result = await new ConfirmEmailHandler(_identity).Handle(
            new ConfirmEmailCommand(UserId.From(3), "code"),
            Cancellation
        );

        result.Status.ShouldBe(ResultStatus.Invalid);
    }

    [Fact]
    public async Task ResendConfirmationAlwaysSucceeds()
    {
        var result = await new ResendConfirmationHandler(_identity).Handle(
            new ResendConfirmationCommand(TestUsers.Email),
            Cancellation
        );

        result.IsSuccess.ShouldBeTrue();
        await _identity
            .Received(1)
            .SendEmailConfirmationAsync(TestUsers.Email, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ForgotPasswordAlwaysSucceeds()
    {
        var result = await new ForgotPasswordHandler(_identity).Handle(
            new ForgotPasswordCommand(TestUsers.Email),
            Cancellation
        );

        result.IsSuccess.ShouldBeTrue();
        await _identity
            .Received(1)
            .SendPasswordResetAsync(TestUsers.Email, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResetPasswordPassesOnTheIdentityResult()
    {
        _identity
            .ResetPasswordAsync(
                TestUsers.Email,
                "code",
                "new-password",
                Arg.Any<CancellationToken>()
            )
            .Returns(Result.Success());

        var result = await new ResetPasswordHandler(_identity).Handle(
            new ResetPasswordCommand(TestUsers.Email, "code", "new-password"),
            Cancellation
        );

        result.IsSuccess.ShouldBeTrue();
    }
}
