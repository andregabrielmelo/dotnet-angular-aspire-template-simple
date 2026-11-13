using Ardalis.Result;
using Microsoft.AspNetCore.Identity;

namespace AppTemplate.Infrastructure.Identity;

public static class IdentityResultExtensions
{
    /// <summary>
    /// Identity's result as the API's: <c>Success</c>, or <c>Error</c> carrying Identity's error
    /// descriptions. An <c>Error</c> answers a generic 500, so the descriptions never reach the
    /// client (ResultExtensions).
    /// </summary>
    public static Result ToApplicationResult(this IdentityResult result)
    {
        // TODO: This can be improved to return a more specific error code or message based on the IdentityResult errors.
        return result.Succeeded
            ? Result.Success()
            : Result.Error(new ErrorList(result.Errors.Select(e => e.Description)));
    }
}
