namespace AppTemplate.UnitTests.Core.UserAggregate;

public class UserCreate
{
    [Fact]
    public void SetsNameEmailAndPasswordHashAsAMember()
    {
        var user = User.Create(TestUsers.Name, TestUsers.Email, "hash");

        user.Name.ShouldBe(TestUsers.Name);
        user.Email.ShouldBe(TestUsers.Email);
        user.PasswordHash.ShouldBe("hash");
        user.Role.ShouldBe(UserRole.Member);
        user.PhoneNumber.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ThrowsGivenEmptyPasswordHash(string passwordHash)
    {
        Should.Throw<ArgumentException>(() =>
            User.Create(TestUsers.Name, TestUsers.Email, passwordHash)
        );
    }

    [Fact]
    public void ThrowsGivenTooLongPasswordHash()
    {
        Should.Throw<ArgumentException>(() =>
            User.Create(
                TestUsers.Name,
                TestUsers.Email,
                new string('x', User.PasswordHashMaxLength + 1)
            )
        );
    }
}
