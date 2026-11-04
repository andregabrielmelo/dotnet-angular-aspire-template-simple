using AppTemplate.UseCases.Authorization;

namespace AppTemplate.UnitTests.UseCases.Authorization;

public class RolePermissionsFor
{
    [Fact]
    public void GivesAnAdminEveryPermission() =>
        RolePermissions.For(UserRole.Admin).ShouldBe(Permission.All);

    [Fact]
    public void GivesAMemberNoPermissionsAsOwnershipIsCheckedByHandlers() =>
        RolePermissions.For(UserRole.Member).ShouldBeEmpty();

    [Fact]
    public void MapsEveryRoleToKnownPermissions()
    {
        foreach (var role in Enum.GetValues<UserRole>())
        {
            RolePermissions
                .For(role)
                .ShouldAllBe(permission => Permission.All.Contains(permission));
        }
    }
}
