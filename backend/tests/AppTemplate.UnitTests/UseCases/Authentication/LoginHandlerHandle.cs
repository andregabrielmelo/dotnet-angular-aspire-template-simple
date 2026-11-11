using AppTemplate.UseCases.Authentication;
using AppTemplate.UseCases.Authentication.Commands.Login;
using AppTemplate.UseCases.Interfaces;

namespace AppTemplate.UnitTests.UseCases.Authentication;

public class LoginHandlerHandle
{
    private readonly IIdentityService _identity = Substitute.For<IIdentityService>();

    private void PasswordCheckReturns(Result<AccountDto> result) =>
        _identity
            .CheckPasswordAsync(TestUsers.Email, "a-password", Arg.Any<CancellationToken>())
            .Returns(result);

    private Task<Result> Login() =>
        new LoginHandler(_identity)
            .Handle(
                new LoginCommand(TestUsers.Email, "a-password"),
                TestContext.Current.CancellationToken
            )
            .AsTask();

    [Fact]
    public async Task SignsInGivenTheRightPassword()
    {
        PasswordCheckReturns(TestUsers.Account(id: 7));

        var result = await Login();

        result.IsSuccess.ShouldBeTrue();
        await _identity.Received(1).SignInAsync(UserId.From(7), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnsUnauthorizedWithoutSigningInWhenTheCredentialsAreRefused()
    {
        PasswordCheckReturns(Result.Unauthorized());

        (await Login()).Status.ShouldBe(ResultStatus.Unauthorized);
        await _identity
            .DidNotReceiveWithAnyArgs()
            .SignInAsync(UserId.From(1), CancellationToken.None);
    }

    [Fact]
    public async Task PassesOnForbiddenForAnUnconfirmedEmail()
    {
        PasswordCheckReturns(Result.Forbidden("Confirm your email address before signing in."));

        var result = await Login();

        result.Status.ShouldBe(ResultStatus.Forbidden);
        result.Errors.ShouldContain("Confirm your email address before signing in.");
        await _identity
            .DidNotReceiveWithAnyArgs()
            .SignInAsync(UserId.From(1), CancellationToken.None);
    }
}
