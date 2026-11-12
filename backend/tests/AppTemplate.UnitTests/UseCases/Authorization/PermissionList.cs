using System.Text.RegularExpressions;
using AppTemplate.UseCases.Authorization;

namespace AppTemplate.UnitTests.UseCases.Authorization;

/// <summary>The permissions as tokens carry them: their values are the claims.</summary>
public class PermissionList
{
    [Fact]
    public void HasUniqueValues() =>
        Permission.List.Select(permission => permission.Value).ShouldBeUnique();

    [Fact]
    public void UsesTheResourceColonActionForm() =>
        Permission.List.ShouldAllBe(permission =>
            Regex.IsMatch(permission.Value, "^[a-z]+:[a-z]+$")
        );

    [Fact]
    public void ConvertsToItsClaimValue()
    {
        string claim = Permission.UsersRead;

        claim.ShouldBe("users:read");
    }
}
