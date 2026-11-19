namespace AppTemplate.Core.Aggregates.UserAggregate.Errors;

/// <summary>
/// Provides predefined errors related to users.
/// </summary>
public static class UserErrors
{
    public static readonly Result EmailNotUnique = Result.Conflict(
        "Users.EmailNotUnique",
        "The provided email is not unique"
    );
}
