using AppTemplate.Core.Aggregates.UserAggregate.Events;
using AppTemplate.Core.Aggregates.UserAggregate.Handlers;

namespace AppTemplate.UnitTests.Core.UserAggregate;

public class UserRegisteredDomainEventHandlerHandle
{
    [Fact]
    public async Task SendsAWelcomeEmailToTheNewUser()
    {
        var emailSender = Substitute.For<IEmailSender>();

        await new UserRegisteredDomainEventHandler(emailSender).Handle(
            new UserRegisteredDomainEvent(UserId.From(5), "ada@example.com"),
            TestContext.Current.CancellationToken
        );

        await emailSender
            .Received(1)
            .SendAsync(
                "ada@example.com",
                "Welcome!",
                Arg.Is<string>(body => body.Contains("Thanks for registering")),
                Arg.Any<CancellationToken>()
            );
    }
}
