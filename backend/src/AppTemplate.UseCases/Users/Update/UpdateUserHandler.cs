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
/// decided here rather than by an endpoint policy. The phone number is updated part by part, like
/// a PATCH (<see cref="MergePhoneNumber"/>), so that is decided here too: whether a part may be
/// left out depends on the saved number. The saved change invalidates the cached user
/// reads of this user (<see cref="CacheTags.User"/>) and the user list (<see cref="CacheTags.UserList"/>).
/// </summary>
public class UpdateUserHandler(
    IRepository<User> repository,
    ICurrentUser currentUser,
    HybridCache cache
) : ICommandHandler<UpdateUserCommand, Result<UserDto>>
{
    public async ValueTask<Result<UserDto>> Handle(
        UpdateUserCommand command,
        CancellationToken cancellationToken
    )
    {
        var user = await repository.GetByIdAsync(command.UserId, cancellationToken);
        if (user == null)
            return Result<UserDto>.NotFound();

        var isOwnProfile = currentUser.Id == user.Id;
        if (!isOwnProfile && !currentUser.HasPermission(Permission.UsersWrite))
            return Result<UserDto>.Forbidden();

        var phoneNumber = MergePhoneNumber(user.PhoneNumber, command, out var missingParts);
        if (missingParts.Count > 0)
        {
            return Result<UserDto>.Invalid(missingParts);
        }

        user.UpdateName(command.UserName);
        if (phoneNumber is not null)
        {
            user.UpdatePhoneNumber(phoneNumber);
        }

        await repository.UpdateAsync(user, cancellationToken);
        await cache.RemoveByTagAsync(
            [CacheTags.User(user.Id), CacheTags.UserList],
            cancellationToken
        );

        var dto = new UserDto(user.Id, user.Name, user.PhoneNumber);
        return Result<UserDto>.Success(dto);
    }

    /// <summary>
    /// The phone number after the update, or <c>null</c> when the command sends no phone field.
    /// Like a PATCH, each part sent replaces the saved one and each part left out keeps it. A
    /// blank country code or number counts as left out, since nothing removes the phone number;
    /// an empty extension removes just the extension. A country code or number that is neither
    /// sent nor saved is reported in <paramref name="missingParts"/>.
    /// </summary>
    private static PhoneNumber? MergePhoneNumber(
        PhoneNumber? saved,
        UpdateUserCommand command,
        out List<ValidationError> missingParts
    )
    {
        missingParts = [];
        var sentCountryCode = string.IsNullOrWhiteSpace(command.PhoneCountryCode)
            ? null
            : command.PhoneCountryCode;
        var sentNumber = string.IsNullOrWhiteSpace(command.PhoneNumber)
            ? null
            : command.PhoneNumber;
        if (sentCountryCode is null && sentNumber is null && command.PhoneExtension is null)
        {
            return null;
        }

        var countryCode = sentCountryCode ?? saved?.CountryCode;
        var number = sentNumber ?? saved?.Number;
        var extension = command.PhoneExtension switch
        {
            null => saved?.Extension,
            "" => null,
            var sent => sent,
        };

        if (countryCode is null)
        {
            missingParts.Add(MissingPart(nameof(command.PhoneCountryCode)));
        }
        if (number is null)
        {
            missingParts.Add(MissingPart(nameof(command.PhoneNumber)));
        }

        return countryCode is null || number is null
            ? null
            : new PhoneNumber(countryCode, number, extension);
    }

    private static ValidationError MissingPart(string identifier) =>
        new()
        {
            Identifier = identifier,
            ErrorMessage = "A phone number needs both its country code and its number.",
        };
}
