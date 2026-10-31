using AppTemplate.Core.Aggregates.UserAggregate.Events;
using AppTemplate.Core.Aggregates.UserAggregate.Specifications;
using AppTemplate.SharedKernel;
using AppTemplate.UnitTests.UseCases.Authentication;
using AppTemplate.UseCases.Authentication;
using AppTemplate.UseCases.Users;
using AppTemplate.UseCases.Users.Register;
using Ardalis.Result;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace AppTemplate.UnitTests.UseCases.Users;

public class RegisterUserHandlerTests
{
    private readonly AuthenticationFixture _auth = new();
    private readonly IDomainEventDispatcher _dispatcher = Substitute.For<IDomainEventDispatcher>();

    private static readonly RegisterUserCommand Command = new(
        UserName.From("Ada Lovelace"),
        new EmailAddress("ada@example.com"),
        "secret-password"
    );

    public RegisterUserHandlerTests()
    {
        // Saving assigns the id, as VogenIdValueGenerator does.
        _auth
            .Users.AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var user = call.Arg<User>();
                user.Id = UserId.From(5);
                return user;
            });
    }

    private Task<Result<AuthSession>> Register() =>
        new RegisterUserHandler(_auth.Users, _auth.PasswordHasher, _dispatcher, _auth.Sessions)
            .Handle(Command, default)
            .AsTask();

    [Fact]
    public async Task NewEmail_SavesAHashedPassword_AnnouncesTheUser_AndSignsIn()
    {
        var result = await Register();

        Assert.True(result.IsSuccess);
        Assert.Equal("refresh-token-1", result.Value.RefreshToken);
        await _auth
            .Users.Received(1)
            .AddAsync(
                Arg.Is<User>(user =>
                    user.PasswordHash == "hashed:secret-password" && user.Role == UserRole.Member
                ),
                Arg.Any<CancellationToken>()
            );
        await _dispatcher
            .Received(1)
            .DispatchAndClearEvents(Arg.Any<IEnumerable<IHasDomainEvents>>());
    }

    [Fact]
    public async Task TheEvent_CarriesTheSavedIdAndEmail()
    {
        UserRegisteredDomainEvent? raised = null;
        await _dispatcher.DispatchAndClearEvents(
            Arg.Do<IEnumerable<IHasDomainEvents>>(entities =>
                raised = entities.Single().DomainEvents.OfType<UserRegisteredDomainEvent>().Single()
            )
        );

        await Register();

        Assert.NotNull(raised);
        Assert.Equal(UserId.From(5), raised.UserId);
        Assert.Equal("ada@example.com", raised.Email);
    }

    [Fact]
    public async Task TakenEmail_IsAConflict_AndSavesNothing()
    {
        _auth
            .Users.AnyAsync(Arg.Any<UserByEmailSpecification>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await Register();

        Assert.Equal(ResultStatus.Conflict, result.Status);
        await _auth.Users.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConcurrentRegistrationOfTheSameEmail_IsAConflict()
    {
        _auth
            .Users.AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(
                new UniqueConstraintViolationException(
                    UserConstraints.EmailUniqueIndex,
                    new Exception()
                )
            );

        var result = await Register();

        Assert.Equal(ResultStatus.Conflict, result.Status);
        await _dispatcher
            .DidNotReceive()
            .DispatchAndClearEvents(Arg.Any<IEnumerable<IHasDomainEvents>>());
    }

    [Fact]
    public async Task OtherUniqueViolations_AreNotTurnedIntoAConflict()
    {
        _auth
            .Users.AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new UniqueConstraintViolationException("pk_users", new Exception()));

        await Assert.ThrowsAsync<UniqueConstraintViolationException>(Register);
    }
}
