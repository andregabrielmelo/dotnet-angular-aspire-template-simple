using AppTemplate.Core.Aggregates.UserAggregate.Events;
using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Interfaces;
using AppTemplate.UseCases.Users.Commands.Register;
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

    public RegisterUserHandlerHandle() => RoleAdditionReturns(Result.Success());

    private void RoleAdditionReturns(Result result) =>
        _identity
            .AddToRolesAsync(UserId.From(1), [], CancellationToken.None)
            .ReturnsForAnyArgs(result);

    private void CreationReturns(Result<UserId> result) =>
        _identity
            .CreateUserAsync(
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
    public async Task PublishesUserRegisteredWithTheIdNameAndEmail()
    {
        CreationReturns(UserId.From(5));

        await Register();

        await _mediator
            .Received(1)
            .Publish(
                Arg.Is<UserRegisteredDomainEvent>(raised =>
                    raised.UserId == UserId.From(5)
                    && raised.Name == TestUsers.Name
                    && raised.Email == TestUsers.Email
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task AddsTheNewAccountToMember()
    {
        CreationReturns(UserId.From(5));

        await Register();

        await _identity
            .Received(1)
            .AddToRolesAsync(
                UserId.From(5),
                Arg.Is<IEnumerable<Role>>(roles => roles.SequenceEqual(new[] { Role.Member })),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task PassesOnAFailedRoleAdditionAndPublishesNothing()
    {
        CreationReturns(UserId.From(5));
        RoleAdditionReturns(Result.Error());

        var result = await Register();

        result.Status.ShouldBe(ResultStatus.Error);
        await _mediator
            .DidNotReceive()
            .Publish(Arg.Any<UserRegisteredDomainEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PassesOnAConflictAndPublishesNothing()
    {
        CreationReturns(UserErrors.EmailNotUnique);

        var result = await Register();

        result.Status.ShouldBe(ResultStatus.Conflict);
        await _identity
            .DidNotReceiveWithAnyArgs()
            .AddToRolesAsync(UserId.From(1), [], CancellationToken.None);
        await _mediator
            .DidNotReceive()
            .Publish(Arg.Any<UserRegisteredDomainEvent>(), Arg.Any<CancellationToken>());
    }
}
