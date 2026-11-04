using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;

namespace AppTemplate.UseCases.Authentication;

/// <param name="SecurityStamp">Changes whenever the credentials do; refresh tokens record it.</param>
public record AccountDto(
    UserId Id,
    EmailAddress Email,
    bool EmailConfirmed,
    IReadOnlyList<string> Roles,
    string SecurityStamp,
    bool IsLockedOut
);
