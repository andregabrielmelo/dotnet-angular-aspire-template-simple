using AppTemplate.UseCases.Authentication;
using AppTemplate.UseCases.Authentication.Commands.CreateToken;
using AppTemplate.UseCases.Interfaces;

namespace AppTemplate.UnitTests.UseCases.Authentication;

public class CreateTokenHandlerHandle
{
    private readonly IIdentityService _identity = Substitute.For<IIdentityService>();
    private readonly ITokenClaimsService _tokens = Substitute.For<ITokenClaimsService>();

    private void PasswordCheckReturns(Result<AccountDto> result) =>
        _identity
            .CheckPasswordAsync(TestUsers.Email, "a-password", Arg.Any<CancellationToken>())
            .Returns(result);

    private Task<Result<AccessToken>> CreateToken() =>
        new CreateTokenHandler(_identity, _tokens)
            .Handle(
                new CreateTokenCommand(TestUsers.Email, "a-password"),
                TestContext.Current.CancellationToken
            )
            .AsTask();

    [Fact]
    public async Task IssuesATokenGivenTheRightPassword()
    {
        PasswordCheckReturns(TestUsers.Account(id: 7));
        var token = new AccessToken("a.signed.token", DateTimeOffset.UnixEpoch);
        _tokens.GetTokenAsync(UserId.From(7), Arg.Any<CancellationToken>()).Returns(token);

        var result = await CreateToken();

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(token);
        await _identity.DidNotReceiveWithAnyArgs().SignInAsync(UserId.From(1), default);
    }

    [Fact]
    public async Task ReturnsUnauthorizedWithoutATokenWhenTheCredentialsAreRefused()
    {
        PasswordCheckReturns(Result.Unauthorized());

        (await CreateToken()).Status.ShouldBe(ResultStatus.Unauthorized);
        await _tokens.DidNotReceiveWithAnyArgs().GetTokenAsync(UserId.From(1), default);
    }

    [Fact]
    public async Task PassesOnForbiddenForAnUnconfirmedEmail()
    {
        PasswordCheckReturns(Result.Forbidden("Confirm your email address before signing in."));

        var result = await CreateToken();

        result.Status.ShouldBe(ResultStatus.Forbidden);
        result.Errors.ShouldContain("Confirm your email address before signing in.");
        await _tokens.DidNotReceiveWithAnyArgs().GetTokenAsync(UserId.From(1), default);
    }
}
