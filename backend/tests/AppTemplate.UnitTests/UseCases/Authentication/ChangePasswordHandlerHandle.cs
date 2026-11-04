using AppTemplate.UseCases.Authentication;
using AppTemplate.UseCases.Authentication.ChangePassword;

namespace AppTemplate.UnitTests.UseCases.Authentication;

public class ChangePasswordHandlerHandle
{
    private readonly IIdentityService _identity = Substitute.For<IIdentityService>();
    private readonly IRepository<User> _users = Substitute.For<IRepository<User>>();

    private void ChangeReturns(Result result) =>
        _identity
            .ChangePasswordAsync(
                UserId.From(1),
                "old-password",
                "new-password",
                Arg.Any<CancellationToken>()
            )
            .Returns(result);

    private Task<Result<AuthenticatedUserDto>> Change() =>
        new ChangePasswordHandler(_identity, _users)
            .Handle(
                new ChangePasswordCommand(UserId.From(1), "old-password", "new-password"),
                TestContext.Current.CancellationToken
            )
            .AsTask();

    [Fact]
    public async Task ReturnsTheUserForTheirNewSession()
    {
        ChangeReturns(Result.Success());
        _identity
            .GetAccountAsync(UserId.From(1), Arg.Any<CancellationToken>())
            .Returns(TestUsers.Account());
        _users
            .GetByIdAsync(UserId.From(1), Arg.Any<CancellationToken>())
            .Returns(TestUsers.Saved());

        var result = await Change();

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(UserId.From(1));
    }

    [Fact]
    public async Task PassesOnAWrongCurrentPassword()
    {
        ChangeReturns(
            Result.Invalid(
                new ValidationError
                {
                    Identifier = "currentPassword",
                    ErrorMessage = "Incorrect password.",
                }
            )
        );

        var result = await Change();

        result.Status.ShouldBe(ResultStatus.Invalid);
        result.ValidationErrors.Single().Identifier.ShouldBe("currentPassword");
    }
}
