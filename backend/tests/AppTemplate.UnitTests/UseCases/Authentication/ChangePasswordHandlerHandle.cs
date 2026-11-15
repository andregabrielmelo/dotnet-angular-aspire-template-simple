using AppTemplate.UseCases.Authentication.ChangePassword;
using AppTemplate.UseCases.Interfaces;

namespace AppTemplate.UnitTests.UseCases.Authentication;

public class ChangePasswordHandlerHandle
{
    private readonly IIdentityService _identity = Substitute.For<IIdentityService>();

    private void ChangeReturns(Result result) =>
        _identity
            .ChangePasswordAsync(
                UserId.From(1),
                "old-password",
                "new-password",
                Arg.Any<CancellationToken>()
            )
            .Returns(result);

    private Task<Result> Change() =>
        new ChangePasswordHandler(_identity)
            .Handle(
                new ChangePasswordCommand(UserId.From(1), "old-password", "new-password"),
                TestContext.Current.CancellationToken
            )
            .AsTask();

    [Fact]
    public async Task KeepsThisSessionSignedIn()
    {
        ChangeReturns(Result.Success());

        var result = await Change();

        result.IsSuccess.ShouldBeTrue();
        await _identity
            .Received(1)
            .RefreshSignInAsync(UserId.From(1), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PassesOnAWrongCurrentPasswordWithoutRefreshingTheSession()
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
        await _identity
            .DidNotReceiveWithAnyArgs()
            .RefreshSignInAsync(UserId.From(1), CancellationToken.None);
    }
}
