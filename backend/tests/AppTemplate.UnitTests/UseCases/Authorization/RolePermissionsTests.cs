using AppTemplate.UseCases.Authorization;

namespace AppTemplate.UnitTests.UseCases.Authorization;

public class RolePermissionsTests
{
    [Fact]
    public void Admin_HasEveryPermission() =>
        Assert.Equal(Permission.All, RolePermissions.For(UserRole.Admin));

    [Fact]
    public void Member_HasNoPermissions_OwnershipIsCheckedByHandlers() =>
        Assert.Empty(RolePermissions.For(UserRole.Member));

    [Fact]
    public void EveryRole_HasAMapping()
    {
        foreach (var role in Enum.GetValues<UserRole>())
        {
            Assert.All(
                RolePermissions.For(role),
                permission => Assert.Contains(permission, Permission.All)
            );
        }
    }
}
