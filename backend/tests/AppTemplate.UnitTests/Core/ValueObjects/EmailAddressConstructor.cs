namespace AppTemplate.UnitTests.Core.ValueObjects;

public class EmailAddressConstructor
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("missing-domain@")]
    public void ThrowsGivenAnInvalidValue(string value)
    {
        Should.Throw<ArgumentException>(() => new EmailAddress(value));
    }

    [Fact]
    public void KeepsAValidAddress()
    {
        new EmailAddress("ada@example.com").Value.ShouldBe("ada@example.com");
    }

    [Fact]
    public void LowerCasesAndTrimsSoCasingMakesNoNewAddress()
    {
        new EmailAddress(" Ada@Example.COM ").ShouldBe(new EmailAddress("ada@example.com"));
    }

    [Theory]
    [InlineData("ada@example.com", true)]
    [InlineData(" Ada@Example.com ", true)]
    [InlineData("ada@example", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidMatchesWhatTheConstructorAccepts(string? value, bool expected)
    {
        EmailAddress.IsValid(value).ShouldBe(expected);
    }
}
