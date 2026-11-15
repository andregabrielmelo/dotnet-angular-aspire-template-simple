using AppTemplate.Core.Aggregates.UserAggregate.Events;
using AppTemplate.Core.Aggregates.UserAggregate.Handlers;
using AppTemplate.Core.Interfaces;

namespace AppTemplate.UnitTests.Core.UserAggregate;

public class UserNameUpdatedEventHandlerHandle
{
    private readonly ILoggerAdapter<UserNameUpdatedEventHandler> _logger = Substitute.For<
        ILoggerAdapter<UserNameUpdatedEventHandler>
    >();

    [Fact]
    public async Task LogsTheUsersIdButNotTheirName()
    {
        var updated = new UserNameUpdatedEvent(UserId.From(5), UserName.From("Grace Hopper"));

        await new UserNameUpdatedEventHandler(_logger).Handle(
            updated,
            TestContext.Current.CancellationToken
        );

        _logger
            .Received(1)
            .LogInformation(
                "User {UserId}'s name was updated",
                Arg.Is<object?[]>(args => args.SequenceEqual(new object?[] { UserId.From(5) }))
            );
    }
}
