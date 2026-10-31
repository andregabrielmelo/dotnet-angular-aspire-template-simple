using AppTemplate.Core.Aggregates.RefreshTokenAggregate;
using AppTemplate.Core.Aggregates.UserAggregate.Specifications;
using AppTemplate.UseCases.Authentication;
using AppTemplate.UseCases.Authentication.Login;
using AppTemplate.UseCases.Authorization;
using Ardalis.Result;
using NSubstitute;

namespace AppTemplate.UnitTests.UseCases.Authentication;

public class LoginHandlerTests
{
    private readonly AuthenticationFixture _auth = new();

    private Task<Result<AuthSession>> Login(string password = "secret-password") =>
        new LoginHandler(_auth.Users, _auth.PasswordHasher, _auth.Sessions)
            .Handle(new LoginCommand(new EmailAddress("ada@example.com"), password), default)
            .AsTask();

    private void UserExists(User? user) =>
        _auth
            .Users.FirstOrDefaultAsync(
                Arg.Any<UserByEmailSpecification>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(user);

    [Fact]
    public async Task UnknownEmail_IsUnauthorized_AfterHashingAnyway()
    {
        UserExists(null);

        var result = await Login();

        Assert.Equal(ResultStatus.Unauthorized, result.Status);
        _auth.PasswordHasher.Received(1).Hash("secret-password");
        await _auth
            .RefreshTokens.DidNotReceive()
            .AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WrongPassword_IsUnauthorized()
    {
        UserExists(AuthenticationFixture.SavedUser());

        var result = await Login("wrong-password");

        Assert.Equal(ResultStatus.Unauthorized, result.Status);
        await _auth
            .RefreshTokens.DidNotReceive()
            .AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RightPassword_StartsASessionWithTheRolesPermissions()
    {
        var admin = AuthenticationFixture.SavedUser(role: UserRole.Admin);
        UserExists(admin);

        var result = await Login();

        Assert.True(result.IsSuccess);
        Assert.Equal("access-token", result.Value.AccessToken);
        Assert.Equal("refresh-token-1", result.Value.RefreshToken);
        _auth.TokenIssuer.Received(1).IssueAccessToken(admin, Permission.All);
        await _auth
            .RefreshTokens.Received(1)
            .AddAsync(
                Arg.Is<RefreshToken>(token =>
                    token.TokenHash == RefreshTokenHash.Of("refresh-token-1")
                    && token.UserId == admin.Id
                ),
                Arg.Any<CancellationToken>()
            );
    }
}
