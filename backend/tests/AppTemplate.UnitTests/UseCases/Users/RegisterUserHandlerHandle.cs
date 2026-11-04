using AppTemplate.Core.Aggregates.UserAggregate.Events;
using AppTemplate.UseCases.Authentication;
using AppTemplate.UseCases.Users.Register;
using Mediator;

namespace AppTemplate.UnitTests.UseCases.Users;

public class RegisterUserHandlerHandle
{
    private readonly IIdentityService _identity = Substitute.For<IIdentityService>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();

    private static readonly RegisterUserCommand Command = new(
        TestUsers.Name,
        TestUsers.Email,
        "secret-password"
    );

    private void CreationReturns(Result<UserId> result) =>
        _identity
            .CreateAccountAsync(
                TestUsers.Name,
                TestUsers.Email,
                "secret-password",
                Arg.Any<CancellationToken>()
            )
            .Returns(result);

    private Task<Result<UserId>> Register() =>
        new RegisterUserHandler(_identity, _mediator)
            .Handle(Command, TestContext.Current.CancellationToken)
            .AsTask();

    [Fact]
    public async Task ReturnsTheNewUsersId()
    {
        CreationReturns(UserId.From(5));

        var result = await Register();

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(UserId.From(5));
    }

    [Fact]
    public async Task PublishesUserRegisteredWithTheNewId()
    {
        CreationReturns(UserId.From(5));

        await Register();

        await _mediator
            .Received(1)
            .Publish(
                Arg.Is<UserRegisteredDomainEvent>(raised => raised.UserId == UserId.From(5)),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task PassesOnAConflictAndPublishesNothing()
    {
        CreationReturns(Result.Conflict("An account with this email already exists"));

        var result = await Register();

        result.Status.ShouldBe(ResultStatus.Conflict);
        await _mediator
            .DidNotReceive()
            .Publish(Arg.Any<UserRegisteredDomainEvent>(), Arg.Any<CancellationToken>());
    }
}
