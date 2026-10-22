using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;
using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Caching;

namespace AppTemplate.UseCases.Users.Update;

/// <summary>
/// Resource-based authorization: anyone may update their own profile, while updating someone
/// else's requires <see cref="Permission.UsersWrite"/>. This needs the loaded user, so it is
/// decided here rather than by an endpoint policy.
/// </summary>
public class UpdateUserHandler(
    IRepository<User> _repository,
    ICurrentUser _currentUser,
    ICacheInvalidator _cacheInvalidator
) : Mediator.ICommandHandler<UpdateUserCommand, Result<UserDto>>
{
    public async ValueTask<Result<UserDto>> Handle(
        UpdateUserCommand command,
        CancellationToken cancellationToken
    )
    {
        var user = await _repository.GetByIdAsync(command.UserId, cancellationToken);
        if (user == null)
            return Result<UserDto>.NotFound();

        var isOwnProfile =
            _currentUser.ExternalId is not null && _currentUser.ExternalId == user.ExternalId;
        if (!isOwnProfile && !_currentUser.HasPermission(Permission.UsersWrite))
            return Result<UserDto>.Forbidden();

        PhoneNumber? phoneNumber = null;
        if (!string.IsNullOrWhiteSpace(command.PhoneNumber))
        {
            if (string.IsNullOrWhiteSpace(command.PhoneCountryCode))
                return Result<UserDto>.Error("Phone country code is required with a phone number");

            phoneNumber = new PhoneNumber(
                command.PhoneCountryCode,
                command.PhoneNumber,
                command.PhoneExtension
            );
        }

        user.UpdateName(command.UserName);
        if (phoneNumber is not null)
        {
            user.UpdatePhoneNumber(phoneNumber);
        }

        await _repository.UpdateAsync(user, cancellationToken);
        await _cacheInvalidator.InvalidateAsync(CacheTags.Users, cancellationToken);

        var dto = new UserDto(user.Id, user.Name, user.PhoneNumber);
        return Result<UserDto>.Success(dto);
    }
}
