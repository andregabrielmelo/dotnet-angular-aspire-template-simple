using AppTemplate.UseCases.Authorization;

namespace AppTemplate.UnitTests.UseCases.Authorization;

/// <summary><see cref="Role.PermissionsFor"/>: what an account's Identity roles grant.</summary>
public class RolePermissionsFor
{
    [Fact]
    public void GivesAnAdminEveryPermission() =>
        Role.PermissionsFor([Role.Admin.Name]).ShouldBe(Permission.List, ignoreOrder: true);

    [Fact]
    public void GivesAMemberNoPermissionsAsOwnershipIsCheckedByHandlers() =>
        Role.PermissionsFor([Role.Member.Name]).ShouldBeEmpty();

    [Fact]
    public void CombinesTheRolesWithoutDuplicates() =>
        Role.PermissionsFor([Role.Admin.Name, Role.Member.Name, Role.Admin.Name])
            .ShouldBe(Permission.List, ignoreOrder: true);

    [Fact]
    public void GrantsNothingForARoleItDoesNotKnow() =>
        Role.PermissionsFor(["Auditor"]).ShouldBeEmpty();

    [Fact]
    public void OrdersThePermissionsByTheirClaim() =>
        Role.PermissionsFor([Role.Admin.Name])
            .Select(permission => permission.Value)
            .ShouldBe(Permission.List.Select(p => p.Value).Order(StringComparer.Ordinal));
}
