using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;
using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Caching;
using AppTemplate.UseCases.Interfaces;
using Microsoft.Extensions.Caching.Hybrid;

namespace AppTemplate.UseCases.Users.Update;

/// <summary>
/// Resource-based authorization: anyone may update their own profile, while updating someone
/// else's requires <see cref="Permission.UsersWrite"/>. This needs the loaded user, so it is
/// decided here rather than by an endpoint policy. The saved change invalidates the cached user
/// reads of this user (<see cref="CacheTags.User"/>) and the user list (<see cref="CacheTags.UserList"/>).
/// </summary>
public class UpdateUserHandler(
    IRepository<User> _repository,
    ICurrentUser _currentUser,
    HybridCache _cache
) : ICommandHandler<UpdateUserCommand, Result<UserDto>>
{
    public async ValueTask<Result<UserDto>> Handle(
        UpdateUserCommand command,
        CancellationToken cancellationToken
    )
    {
        var user = await _repository.GetByIdAsync(command.UserId, cancellationToken);
        if (user == null)
            return Result<UserDto>.NotFound();

        var isOwnProfile = _currentUser.Id == user.Id;
        if (!isOwnProfile && !_currentUser.HasPermission(Permission.UsersWrite))
            return Result<UserDto>.Forbidden();

        // A phone number needs both its country code and its number. One without the other is
        // invalid, keyed by the missing field; with neither, the phone number isn't changed.
        var hasCountryCode = !string.IsNullOrWhiteSpace(command.PhoneCountryCode);
        var hasNumber = !string.IsNullOrWhiteSpace(command.PhoneNumber);
        if (hasCountryCode != hasNumber)
        {
            return Result<UserDto>.Invalid(
                new ValidationError
                {
                    Identifier = hasNumber
                        ? nameof(command.PhoneCountryCode)
                        : nameof(command.PhoneNumber),
                    ErrorMessage = "A phone number needs both its country code and its number.",
                }
            );
        }

        user.UpdateName(command.UserName);
        if (hasCountryCode && hasNumber)
        {
            user.UpdatePhoneNumber(
                new PhoneNumber(
                    command.PhoneCountryCode!,
                    command.PhoneNumber!,
                    command.PhoneExtension
                )
            );
        }

        await _repository.UpdateAsync(user, cancellationToken);
        await _cache.RemoveByTagAsync(
            [CacheTags.User(user.Id), CacheTags.UserList],
            cancellationToken
        );

        var dto = new UserDto(user.Id, user.Name, user.PhoneNumber);
        return Result<UserDto>.Success(dto);
    }
}
