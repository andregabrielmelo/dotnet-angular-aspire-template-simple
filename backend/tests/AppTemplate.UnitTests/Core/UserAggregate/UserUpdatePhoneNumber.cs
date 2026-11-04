namespace AppTemplate.UnitTests.Core.UserAggregate;

public class UserUpdatePhoneNumber
{
    [Fact]
    public void SetsThePhoneNumber()
    {
        var user = TestUsers.Saved();
        var phoneNumber = new PhoneNumber("+1", "5551234", null);

        user.UpdatePhoneNumber(phoneNumber);

        user.PhoneNumber.ShouldBe(phoneNumber);
    }
}
