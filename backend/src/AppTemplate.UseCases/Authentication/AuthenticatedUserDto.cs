using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;

namespace AppTemplate.UseCases.Authentication;

/// <summary>Who a token is issued for: what goes into its claims and permissions.</summary>
public record AuthenticatedUserDto(UserId Id, UserName Name, EmailAddress Email, UserRole Role)
{
    public static AuthenticatedUserDto FromEntity(User user) =>
        new(user.Id, user.Name, user.Email, user.Role);
}
