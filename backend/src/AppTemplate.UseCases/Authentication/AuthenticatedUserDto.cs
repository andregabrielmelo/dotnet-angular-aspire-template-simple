using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;

namespace AppTemplate.UseCases.Authentication;

/// <summary>Who a token is issued for: what goes into its claims, roles and permissions.</summary>
public record AuthenticatedUserDto(
    UserId Id,
    UserName Name,
    EmailAddress Email,
    IReadOnlyList<string> Roles
)
{
    public static AuthenticatedUserDto From(User profile, AccountDto account) =>
        new(profile.Id, profile.Name, account.Email, account.Roles);
}
