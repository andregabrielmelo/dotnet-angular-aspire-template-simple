using Ardalis.Result;

namespace AppTemplate.Infrastructure.Identity;

public static class IdentityErrors
{
    public static readonly Result InvalidCodeResult = Result.Invalid(
        new ValidationError
        {
            Identifier = "code",
            ErrorMessage = "The link is invalid or has expired. Ask for a new one.",
        }
    );
}
