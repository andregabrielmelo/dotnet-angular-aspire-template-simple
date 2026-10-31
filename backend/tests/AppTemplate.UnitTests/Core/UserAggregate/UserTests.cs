using AppTemplate.Core.Aggregates.UserAggregate.Events;

namespace AppTemplate.UnitTests.Core.UserAggregate;

public class UserTests
{
    private static User CreateUser() =>
        User.Create(UserName.From("Ada Lovelace"), new EmailAddress("ada@example.com"), "hash");

    [Fact]
    public void Create_SetsNameEmailAndHash_AsAMember()
    {
        var user = CreateUser();

        Assert.Equal("Ada Lovelace", user.Name.Value);
        Assert.Equal("ada@example.com", user.Email.Value);
        Assert.Equal("hash", user.PasswordHash);
        Assert.Equal(UserRole.Member, user.Role);
        Assert.Null(user.PhoneNumber);
        Assert.Empty(user.DomainEvents);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyPasswordHash_Throws(string passwordHash)
    {
        Assert.Throws<ArgumentException>(() =>
            User.Create(
                UserName.From("Ada Lovelace"),
                new EmailAddress("ada@example.com"),
                passwordHash
            )
        );
    }

    [Fact]
    public void Create_WithTooLongPasswordHash_Throws()
    {
        var passwordHash = new string('x', User.PasswordHashMaxLength + 1);

        Assert.Throws<ArgumentException>(() =>
            User.Create(
                UserName.From("Ada Lovelace"),
                new EmailAddress("ada@example.com"),
                passwordHash
            )
        );
    }

    [Fact]
    public void MarkRegistered_BeforeTheUserHasAnId_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => CreateUser().MarkRegistered());
    }

    [Fact]
    public void MarkRegistered_RaisesUserRegisteredWithTheIdAndEmail()
    {
        var user = CreateUser();
        user.Id = UserId.From(7); // as if saved

        user.MarkRegistered();

        var registered = Assert.IsType<UserRegisteredDomainEvent>(Assert.Single(user.DomainEvents));
        Assert.Equal(UserId.From(7), registered.UserId);
        Assert.Equal("ada@example.com", registered.Email);
    }

    [Fact]
    public void ChangeRole_ChangesTheRole()
    {
        var user = CreateUser();

        user.ChangeRole(UserRole.Admin);

        Assert.Equal(UserRole.Admin, user.Role);
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
