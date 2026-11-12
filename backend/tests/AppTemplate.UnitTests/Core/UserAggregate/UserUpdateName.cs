namespace AppTemplate.UnitTests.Core.UserAggregate;

public class UserUpdateName
{
    [Fact]
    public void ChangesTheName()
    {
        var user = TestUsers.Saved();
        var newName = UserName.From("Grace Hopper");

        user.UpdateName(newName);

        user.Name.ShouldBe(newName);
    }

    [Fact]
    public void ReturnsTheSameInstanceGivenTheSameName()
    {
        var user = TestUsers.Saved();

        user.UpdateName(user.Name).ShouldBeSameAs(user);
    }
}
