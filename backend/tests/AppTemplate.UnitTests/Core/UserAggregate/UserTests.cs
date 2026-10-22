namespace AppTemplate.UnitTests.Core.UserAggregate;

public class UserTests
{
    private static User CreateUser() =>
        User.Create(
            "keycloak-sub-1",
            UserName.From("Ada Lovelace"),
            new EmailAddress("ada@example.com")
        );

    [Fact]
    public void Create_SetsExternalIdNameAndEmail()
    {
        var user = CreateUser();

        Assert.Equal("keycloak-sub-1", user.ExternalId);
        Assert.Equal("Ada Lovelace", user.Name.Value);
        Assert.Equal("ada@example.com", user.Email.Value);
        Assert.Null(user.PhoneNumber);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyExternalId_Throws(string externalId)
    {
        Assert.Throws<ArgumentException>(() =>
            User.Create(
                externalId,
                UserName.From("Ada Lovelace"),
                new EmailAddress("ada@example.com")
            )
        );
    }

    [Fact]
    public void Create_WithTooLongExternalId_Throws()
    {
        var externalId = new string('x', User.ExternalIdMaxLength + 1);

        Assert.Throws<ArgumentException>(() =>
            User.Create(
                externalId,
                UserName.From("Ada Lovelace"),
                new EmailAddress("ada@example.com")
            )
        );
    }

    [Fact]
    public void UpdateName_WithNewName_ChangesName()
    {
        var user = CreateUser();
        var newName = UserName.From("Grace Hopper");

        user.UpdateName(newName);

        Assert.Equal(newName, user.Name);
    }

    [Fact]
    public void UpdateName_WithSameName_ReturnsSameInstance()
    {
        var user = CreateUser();

        var result = user.UpdateName(user.Name);

        Assert.Same(user, result);
    }

    [Fact]
    public void UpdatePhoneNumber_SetsPhoneNumber()
    {
        var user = CreateUser();
        var phoneNumber = new PhoneNumber("+1", "5551234", null);

        user.UpdatePhoneNumber(phoneNumber);

        Assert.Equal(phoneNumber, user.PhoneNumber);
    }
}
