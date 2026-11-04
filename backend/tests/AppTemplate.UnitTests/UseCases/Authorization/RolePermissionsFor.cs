using AppTemplate.UseCases.Authorization;

namespace AppTemplate.UnitTests.UseCases.Authorization;

public class RolePermissionsFor
{
    [Fact]
    public void GivesAnAdminEveryPermission() =>
        RolePermissions.For([Roles.Admin]).ShouldBe(Permission.All, ignoreOrder: true);

    [Fact]
    public void GivesAMemberNoPermissionsAsOwnershipIsCheckedByHandlers() =>
        RolePermissions.For([Roles.Member]).ShouldBeEmpty();

    [Fact]
    public void CombinesTheRolesWithoutDuplicates() =>
        RolePermissions
            .For([Roles.Admin, Roles.Member, Roles.Admin])
            .ShouldBe(Permission.All, ignoreOrder: true);

    [Fact]
    public void GrantsNothingForARoleItDoesNotKnow() =>
        RolePermissions.For(["Auditor"]).ShouldBeEmpty();

    [Fact]
    public void MapsEveryRoleToKnownPermissions()
    {
        foreach (var role in Roles.All)
        {
            RolePermissions
                .For([role])
                .ShouldAllBe(permission => Permission.All.Contains(permission));
        }
    }
}
