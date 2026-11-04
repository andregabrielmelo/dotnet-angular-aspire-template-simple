using AppTemplate.Core.Aggregates.UserAggregate.Specifications;
using AppTemplate.UseCases.Authentication;
using AppTemplate.UseCases.Authentication.Login;

namespace AppTemplate.UnitTests.UseCases.Authentication;

public class LoginHandlerHandle
{
    private readonly IRepository<User> _users = Substitute.For<IRepository<User>>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly LoginHandler _handler;

    public LoginHandlerHandle()
    {
        _passwordHasher.Verify("hash", "right-password").Returns(true);
        _handler = new LoginHandler(_users, _passwordHasher);
    }

    private void UserExists(User? user) =>
        _users
            .FirstOrDefaultAsync(Arg.Any<UserByEmailSpecification>(), Arg.Any<CancellationToken>())
            .Returns(user);

    private Task<Result<AuthenticatedUserDto>> Login(string password) =>
        _handler
            .Handle(
                new LoginCommand(TestUsers.Email, password),
                TestContext.Current.CancellationToken
            )
            .AsTask();

    [Fact]
    public async Task ReturnsTheUserGivenTheRightPassword()
    {
        UserExists(TestUsers.Saved(id: 7, role: UserRole.Admin));

        var result = await Login("right-password");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(UserId.From(7));
        result.Value.Role.ShouldBe(UserRole.Admin);
    }

    [Fact]
    public async Task ReturnsUnauthorizedGivenAWrongPassword()
    {
        UserExists(TestUsers.Saved());

        var result = await Login("wrong-password");

        result.Status.ShouldBe(ResultStatus.Unauthorized);
    }

    [Fact]
    public async Task ReturnsUnauthorizedGivenAnUnknownEmailAfterHashingAnyway()
    {
        UserExists(null);

        var result = await Login("any-password");

        result.Status.ShouldBe(ResultStatus.Unauthorized);
        _passwordHasher.Received(1).Hash("any-password");
    }
}
