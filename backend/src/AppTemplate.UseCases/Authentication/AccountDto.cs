using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;

namespace AppTemplate.UseCases.Authentication;

/// <summary>A person's ASP.NET Core Identity account, as the use cases see it.</summary>
/// <param name="Roles">Identity role names; <see cref="Authorization.Role.PermissionsFor"/> maps them.</param>
public record AccountDto(
    UserId Id,
    EmailAddress Email,
    bool EmailConfirmed,
    IReadOnlyList<string> Roles
);
