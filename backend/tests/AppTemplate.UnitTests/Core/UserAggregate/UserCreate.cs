namespace AppTemplate.UnitTests.Core.UserAggregate;

public class UserCreate
{
    [Fact]
    public void SetsTheNameWithNoPhoneNumber()
    {
        var user = User.Create(TestUsers.Name);

        user.Name.ShouldBe(TestUsers.Name);
        user.PhoneNumber.ShouldBeNull();
    }
}
