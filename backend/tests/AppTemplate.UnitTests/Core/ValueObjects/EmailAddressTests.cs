namespace AppTemplate.UnitTests.Core.ValueObjects;

public class EmailAddressTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("missing-domain@")]
    public void Constructor_WithInvalidValue_Throws(string value)
    {
        Assert.Throws<ArgumentException>(() => new EmailAddress(value));
    }

    [Fact]
    public void Constructor_WithValidValue_Succeeds()
    {
        var email = new EmailAddress("ada@example.com");

        Assert.Equal("ada@example.com", email.Value);
    }

    [Fact]
    public void Equals_SameValue_AreEqual()
    {
        Assert.Equal(new EmailAddress("ada@example.com"), new EmailAddress("ada@example.com"));
    }

    [Fact]
    public void Constructor_LowerCasesAndTrims_SoCasingDoesNotMakeANewAddress()
    {
        Assert.Equal(new EmailAddress("ada@example.com"), new EmailAddress(" Ada@Example.COM "));
    }

    [Theory]
    [InlineData("ada@example.com", true)]
    [InlineData(" Ada@Example.com ", true)]
    [InlineData("ada@example", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValid_MatchesWhatTheConstructorAccepts(string? value, bool expected)
    {
        Assert.Equal(expected, EmailAddress.IsValid(value));
    }
}
