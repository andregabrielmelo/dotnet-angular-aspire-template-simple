namespace AppTemplate.UnitTests.Core.UserAggregate;

public class UserChangeRole
{
    [Fact]
    public void ChangesTheRole()
    {
        var user = TestUsers.Saved();

        user.ChangeRole(UserRole.Admin);

        user.Role.ShouldBe(UserRole.Admin);
    }
}
