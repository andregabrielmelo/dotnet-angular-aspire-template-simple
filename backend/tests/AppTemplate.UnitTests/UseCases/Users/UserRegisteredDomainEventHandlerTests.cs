using AppTemplate.Core.Aggregates.UserAggregate.Events;
using AppTemplate.UseCases.Users.Register;
using NSubstitute;

namespace AppTemplate.UnitTests.UseCases.Users;

public class UserRegisteredDomainEventHandlerTests
{
    [Fact]
    public async Task Handle_SendsAWelcomeEmailToTheNewUser()
    {
        var emailSender = Substitute.For<IEmailSender>();

        await new UserRegisteredDomainEventHandler(emailSender).Handle(
            new UserRegisteredDomainEvent(UserId.From(5), "ada@example.com"),
            CancellationToken.None
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
