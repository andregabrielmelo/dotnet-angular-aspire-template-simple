namespace AppTemplate.UnitTests.Core.UserAggregate;

public class UserNameFrom
{
    [Fact]
    public void CreatesGivenAValidName()
    {
        UserName.From("Ada Lovelace").Value.ShouldBe("Ada Lovelace");
    }

    [Fact]
    public void ThrowsGivenAnEmptyName()
    {
        Should.Throw<Vogen.ValueObjectValidationException>(() => UserName.From(string.Empty));
    }

    [Fact]
    public void ThrowsGivenANameLongerThanMaxLength()
    {
        Should.Throw<Vogen.ValueObjectValidationException>(() =>
            UserName.From(new string('a', UserName.MaxLength + 1))
        );
    }
}
