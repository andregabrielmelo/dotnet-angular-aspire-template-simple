namespace AppTemplate.UnitTests.Core.ValueObjects;

public class PhoneNumberConstructor
{
    [Theory]
    [InlineData("", "5551234")]
    [InlineData("   ", "5551234")]
    [InlineData("+1", "")]
    [InlineData("+1", "   ")]
    public void ThrowsGivenABlankCountryCodeOrNumber(string countryCode, string number)
    {
        Should.Throw<ArgumentException>(() => new PhoneNumber(countryCode, number, null));
    }

    [Fact]
    public void ThrowsGivenNoCountryCodeOrNumber()
    {
        Should.Throw<ArgumentNullException>(() => new PhoneNumber(null!, "5551234", null));
        Should.Throw<ArgumentNullException>(() => new PhoneNumber("+1", null!, null));
    }

    [Fact]
    public void KeepsItsParts()
    {
        var phoneNumber = new PhoneNumber("+55", "11 98765 4321", "12");

        phoneNumber.CountryCode.ShouldBe("+55");
        phoneNumber.Number.ShouldBe("11 98765 4321");
        phoneNumber.Extension.ShouldBe("12");
        phoneNumber.ToString().ShouldBe("+55 11 98765 4321 x12");
    }
}
