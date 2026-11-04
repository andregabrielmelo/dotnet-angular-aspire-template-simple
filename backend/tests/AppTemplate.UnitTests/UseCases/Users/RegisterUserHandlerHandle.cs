using AppTemplate.Core.Aggregates.UserAggregate.Events;
using AppTemplate.Core.Aggregates.UserAggregate.Specifications;
using AppTemplate.UseCases.Authentication;
using AppTemplate.UseCases.Users.Register;
using Mediator;

namespace AppTemplate.UnitTests.UseCases.Users;

public class RegisterUserHandlerHandle
{
    private readonly IRepository<User> _users = Substitute.For<IRepository<User>>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();

    private static readonly RegisterUserCommand Command = new(
        TestUsers.Name,
        TestUsers.Email,
        "secret-password"
    );

    public RegisterUserHandlerHandle()
    {
        _passwordHasher.Hash("secret-password").Returns("hashed-password");
        // Saving assigns the database identity.
        _users
            .AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var user = call.Arg<User>();
                user.Id = UserId.From(5);
                return user;
            });
    }

    private Task<Result<AuthenticatedUserDto>> Register() =>
        new RegisterUserHandler(_users, _passwordHasher, _mediator)
            .Handle(Command, TestContext.Current.CancellationToken)
            .AsTask();

    [Fact]
    public async Task SavesAMemberWithTheHashedPassword()
    {
        var result = await Register();

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(UserId.From(5));
        await _users
            .Received(1)
            .AddAsync(
                Arg.Is<User>(user =>
                    user.PasswordHash == "hashed-password" && user.Role == UserRole.Member
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task PublishesUserRegisteredWithTheSavedIdAndEmail()
    {
        await Register();

        await _mediator
            .Received(1)
            .Publish(
                Arg.Is<UserRegisteredDomainEvent>(raised =>
                    raised.UserId == UserId.From(5) && raised.Email == "ada@example.com"
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ReturnsConflictGivenATakenEmail()
    {
        _users
            .AnyAsync(Arg.Any<UserByEmailSpecification>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await Register();

        result.Status.ShouldBe(ResultStatus.Conflict);
        await _users.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }
}
