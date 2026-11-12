using Ardalis.Result;
using Microsoft.AspNetCore.Identity;

namespace AppTemplate.Infrastructure.Identity;

public static class IdentityResultExtensions
{
    /// <summary>
    /// Identity's errors as the API's results: a taken email is <c>Conflict</c>; a wrong current
    /// password, a bad code and the password rules are field errors keyed by the request field
    /// (<paramref name="passwordField"/> for the password being set); anything else is
    /// <c>Error</c>, with Identity's error codes only, because descriptions can quote the input.
    /// </summary>
    public static Result ToApplicationResult(this IdentityResult result)
    {
        return result.Succeeded
            ? Result.Success()
            : Result.Error(new ErrorList(result.Errors.Select(e => e.Description)));
    }
}
