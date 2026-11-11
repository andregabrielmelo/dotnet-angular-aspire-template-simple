namespace AppTemplate.Core.Aggregates.UserAggregate;

public static class UserErrors
{
    public static Result NotFound(Guid userId) =>
        Result.NotFound("Users.NotFound", $"The user with the Id = '{userId}' was not found");

    public static readonly Result Unauthorized = Result.Unauthorized(
        "Users.Unauthorized",
        "You are not authorized to perform this action."
    );

    public static readonly Result NotFoundByEmail = Result.NotFound(
        "Users.NotFoundByEmail",
        "The user with the specified email was not found"
    );

    public static readonly Result EmailNotUnique = Result.Conflict(
        "Users.EmailNotUnique",
        "The provided email is not unique"
    );
}
