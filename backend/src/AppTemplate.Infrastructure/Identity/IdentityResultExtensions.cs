using AppTemplate.Core.Aggregates.UserAggregate;
using Ardalis.Result;
using Microsoft.AspNetCore.Identity;

namespace AppTemplate.Infrastructure.Identity;

public static class IdentityResultExtensions
{
    /// <summary>
    /// Identity's result as the API's: <c>Success</c>, or <c>Error</c> carrying Identity's error
    /// descriptions. An <c>Error</c> answers a generic 500, so the descriptions never reach the
    /// client (ResultExtensions). For refusals the client can act on, use the overload that names
    /// the password field.
    /// </summary>
    public static Result ToApplicationResult(this IdentityResult result) =>
        result.Succeeded
            ? Result.Success()
            : Result.Error(new ErrorList(result.Errors.Select(e => e.Description)));

    /// <summary>
    /// Identity's result as the API's, with the refusals a client can act on mapped to their own
    /// answers instead of a 500:
    /// <list type="bullet">
    /// <item>a taken email (<c>DuplicateEmail</c>, <c>DuplicateUserName</c>) →
    /// <see cref="UserErrors.EmailNotUnique"/> (409);</item>
    /// <item>a wrong or expired code (<c>InvalidToken</c>) →
    /// <see cref="IdentityErrors.InvalidCodeResult"/> (400 on <c>code</c>);</item>
    /// <item>a wrong current password (<c>PasswordMismatch</c>) → 400 on <c>currentPassword</c>;</item>
    /// <item>a password Identity's rules refuse (<c>PasswordTooShort</c>, <c>PasswordRequires…</c>) →
    /// 400 on <paramref name="passwordField"/>, with Identity's description.</item>
    /// </list>
    /// Anything else stays a generic <c>Error</c>.
    /// </summary>
    /// <param name="result">Identity's result.</param>
    /// <param name="passwordField">The request field that carried the new password.</param>
    public static Result ToApplicationResult(this IdentityResult result, string passwordField)
    {
        if (result.Succeeded)
        {
            return Result.Success();
        }

        var codes = result.Errors.Select(error => error.Code).ToHashSet(StringComparer.Ordinal);
        if (
            codes.Contains(nameof(IdentityErrorDescriber.DuplicateEmail))
            || codes.Contains(nameof(IdentityErrorDescriber.DuplicateUserName))
        )
        {
            return UserErrors.EmailNotUnique;
        }

        if (codes.Contains(nameof(IdentityErrorDescriber.InvalidToken)))
        {
            return IdentityErrors.InvalidCodeResult;
        }

        var validationErrors = result
            .Errors.Select(error => ToValidationError(error, passwordField))
            .ToList();
        return validationErrors.All(error => error is not null)
            ? Result.Invalid([.. validationErrors.OfType<ValidationError>()])
            : result.ToApplicationResult();
    }

    private static ValidationError? ToValidationError(IdentityError error, string passwordField) =>
        error.Code switch
        {
            nameof(IdentityErrorDescriber.PasswordMismatch) => new ValidationError
            {
                Identifier = "currentPassword",
                ErrorMessage = "The current password is wrong.",
            },
            _ when error.Code.StartsWith("Password", StringComparison.Ordinal) =>
                new ValidationError
                {
                    Identifier = passwordField,
                    ErrorMessage = error.Description,
                },
            _ => null,
        };
}
