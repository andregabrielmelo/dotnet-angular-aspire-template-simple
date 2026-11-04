using AppTemplate.UseCases.Authentication;
using AppTemplate.UseCases.Authentication.Login;
using AppTemplate.UseCases.Authorization;

namespace AppTemplate.UnitTests.UseCases.Authentication;

public class LoginHandlerHandle
{
    private readonly IIdentityService _identity = Substitute.For<IIdentityService>();
    private readonly IRepository<User> _users = Substitute.For<IRepository<User>>();

    private void PasswordCheckReturns(Result<AccountDto> result) =>
        _identity
            .CheckPasswordAsync(TestUsers.Email, "a-password", Arg.Any<CancellationToken>())
            .Returns(result);

    private Task<Result<AuthenticatedUserDto>> Login() =>
        new LoginHandler(_identity, _users)
            .Handle(
                new LoginCommand(TestUsers.Email, "a-password"),
                TestContext.Current.CancellationToken
            )
            .AsTask();

    [Fact]
    public async Task ReturnsTheUserWithTheirRolesGivenTheRightPassword()
    {
        PasswordCheckReturns(TestUsers.Account(id: 7, role: Roles.Admin));
        _users
            .GetByIdAsync(UserId.From(7), Arg.Any<CancellationToken>())
            .Returns(TestUsers.Saved(7));

        var result = await Login();

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(UserId.From(7));
        result.Value.Name.ShouldBe(TestUsers.Name);
        result.Value.Email.ShouldBe(TestUsers.Email);
        result.Value.Roles.ShouldBe([Roles.Admin]);
    }

    [Fact]
    public async Task ReturnsUnauthorizedWhenTheCredentialsAreRefused()
    {
        PasswordCheckReturns(Result.Unauthorized());

        (await Login()).Status.ShouldBe(ResultStatus.Unauthorized);
    }

    [Fact]
    public async Task PassesOnForbiddenForAnUnconfirmedEmail()
    {
        PasswordCheckReturns(Result.Forbidden("Confirm your email address before signing in."));

        var result = await Login();

        result.Status.ShouldBe(ResultStatus.Forbidden);
        result.Errors.ShouldContain("Confirm your email address before signing in.");
    }

    [Fact]
    public async Task ReturnsUnauthorizedWhenTheProfileIsGone()
    {
        PasswordCheckReturns(TestUsers.Account(id: 7));
        _users.GetByIdAsync(UserId.From(7), Arg.Any<CancellationToken>()).Returns((User?)null);

        (await Login()).Status.ShouldBe(ResultStatus.Unauthorized);
    }
}
