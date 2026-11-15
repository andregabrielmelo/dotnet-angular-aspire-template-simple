using AppTemplate.Infrastructure.Identity;
using Ardalis.Result;
using Microsoft.AspNetCore.Identity;

namespace AppTemplate.IntegrationTests.Identity;

/// <summary>
/// The refusals a client can act on become 409 or 400 results; anything else stays a generic
/// error. No database involved.
/// </summary>
public class IdentityResultExtensionsToApplicationResult
{
    private static readonly IdentityErrorDescriber Describer = new();

    private static Result Map(params IdentityError[] errors) =>
        IdentityResult.Failed(errors).ToApplicationResult(passwordField: "newPassword");

    [Fact]
    public void IsSuccessForASuccess()
    {
        IdentityResult.Success.ToApplicationResult("newPassword").IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void IsConflictForATakenEmail()
    {
        Map(Describer.DuplicateEmail("ada@example.com"), Describer.DuplicateUserName("ada"))
            .Status.ShouldBe(ResultStatus.Conflict);
    }

    [Fact]
    public void IsTheInvalidCodeResultForAWrongCode()
    {
        Map(Describer.InvalidToken()).ShouldBe(IdentityErrors.InvalidCodeResult);
    }

    [Fact]
    public void IsInvalidOnCurrentPasswordForAWrongOne()
    {
        var result = Map(Describer.PasswordMismatch());

        result.Status.ShouldBe(ResultStatus.Invalid);
        result.ValidationErrors.Single().Identifier.ShouldBe("currentPassword");
    }

    [Fact]
    public void IsInvalidOnThePasswordFieldForARefusedPassword()
    {
        var result = Map(Describer.PasswordTooShort(8), Describer.PasswordRequiresDigit());

        result.Status.ShouldBe(ResultStatus.Invalid);
        result
            .ValidationErrors.Select(error => error.Identifier)
            .ShouldAllBe(id => id == "newPassword");
        result.ValidationErrors.Count().ShouldBe(2);
    }

    [Fact]
    public void StaysAnErrorForAnythingElse()
    {
        Map(Describer.DefaultError(), Describer.PasswordTooShort(8))
            .Status.ShouldBe(ResultStatus.Error);
    }
}
