using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.Aggregates.UserAggregate.Specifications;
using AppTemplate.UseCases.Authentication;

namespace AppTemplate.UseCases.Users.Register;

public class RegisterUserHandler(
    IRepository<User> _users,
    IPasswordHasher _passwordHasher,
    IDomainEventDispatcher _domainEventDispatcher,
    SessionFactory _sessions
) : ICommandHandler<RegisterUserCommand, Result<AuthSession>>
{
    public const string EmailTakenMessage = "An account with this email already exists";

    public async ValueTask<Result<AuthSession>> Handle(
        RegisterUserCommand command,
        CancellationToken cancellationToken
    )
    {
        if (await _users.AnyAsync(new UserByEmailSpecification(command.Email), cancellationToken))
        {
            return Result.Conflict(EmailTakenMessage);
        }

        var user = User.Create(command.Name, command.Email, _passwordHasher.Hash(command.Password));
        try
        {
            await _users.AddAsync(user, cancellationToken);
        }
        catch (UniqueConstraintViolationException exception)
            when (exception.ConstraintName == UserConstraints.EmailUniqueIndex)
        {
            // A concurrent registration with the same email won the race past the check above.
            return Result.Conflict(EmailTakenMessage);
        }

        // The user is saved and has its id, which the event carries. Dispatched here rather
        // than by a later save, and best-effort: a failing handler doesn't fail registration.
        user.MarkRegistered();
        await _domainEventDispatcher.DispatchAndClearEvents([user]);

        return await _sessions.StartAsync(user, cancellationToken);
    }
}
