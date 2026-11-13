using AppTemplate.Core.Aggregates.UserAggregate.Events;
using AppTemplate.Core.Aggregates.UserAggregate.Handlers;
using AppTemplate.Core.Interfaces;

namespace AppTemplate.UnitTests.Core.UserAggregate;

public class UserRegisteredEventHandlerHandle
{
    private readonly ILoggerAdapter<UserRegisteredEventHandler> _logger = Substitute.For<
        ILoggerAdapter<UserRegisteredEventHandler>
    >();

    [Fact]
    public async Task LogsTheNewUsersIdOnly()
    {
        var registered = new UserRegisteredDomainEvent(
            UserId.From(5),
            UserName.From("Ada Lovelace"),
            new EmailAddress("ada@example.com")
        );

        await new UserRegisteredEventHandler(_logger).Handle(
            registered,
            TestContext.Current.CancellationToken
        );

        _logger
            .Received(1)
            .LogInformation(
                "User {UserId} registered",
                Arg.Is<object?[]>(args => args.SequenceEqual(new object?[] { UserId.From(5) }))
            );
    }
}
