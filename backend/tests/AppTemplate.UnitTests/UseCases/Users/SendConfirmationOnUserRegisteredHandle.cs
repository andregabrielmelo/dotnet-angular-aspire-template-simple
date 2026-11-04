using AppTemplate.Core.Aggregates.UserAggregate.Events;
using AppTemplate.UseCases.Authentication;
using AppTemplate.UseCases.Users.Register;

namespace AppTemplate.UnitTests.UseCases.Users;

public class SendConfirmationOnUserRegisteredHandle
{
    [Fact]
    public async Task EmailsTheNewUserTheirConfirmationLink()
    {
        var identity = Substitute.For<IIdentityService>();

        await new SendConfirmationOnUserRegistered(identity).Handle(
            new UserRegisteredDomainEvent(UserId.From(5)),
            TestContext.Current.CancellationToken
        );

        await identity
            .Received(1)
            .SendEmailConfirmationAsync(UserId.From(5), Arg.Any<CancellationToken>());
    }
}
