using AppTemplate.UseCases.Authentication.Logout;
using AppTemplate.UseCases.Interfaces;

namespace AppTemplate.UnitTests.UseCases.Authentication;

public class LogoutHandlerHandle
{
    [Fact]
    public async Task SignsOut()
    {
        var identity = Substitute.For<IIdentityService>();

        var result = await new LogoutHandler(identity).Handle(
            new LogoutCommand(),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.ShouldBeTrue();
        await identity.Received(1).SignOutAsync();
    }
}
