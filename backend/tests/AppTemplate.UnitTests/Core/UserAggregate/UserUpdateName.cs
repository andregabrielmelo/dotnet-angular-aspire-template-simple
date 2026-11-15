using AppTemplate.Core.Aggregates.UserAggregate.Events;

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

    [Fact]
    public void RaisesUserNameUpdatedWithTheIdAndNewName()
    {
        var user = TestUsers.Saved(id: 7);
        var newName = UserName.From("Grace Hopper");

        user.UpdateName(newName);

        var raised = user
            .DomainEvents.ShouldHaveSingleItem()
            .ShouldBeOfType<UserNameUpdatedEvent>();
        raised.UserId.ShouldBe(UserId.From(7));
        raised.NewName.ShouldBe(newName);
    }

    [Fact]
    public void RaisesNothingGivenTheSameName()
    {
        var user = TestUsers.Saved();

        user.UpdateName(user.Name);

        user.DomainEvents.ShouldBeEmpty();
    }
}
