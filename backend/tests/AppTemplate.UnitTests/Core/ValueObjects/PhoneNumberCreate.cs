namespace AppTemplate.UnitTests.Core.ValueObjects;

public class PhoneNumberCreate
{
    [Theory]
    [InlineData("", "5551234")]
    [InlineData("   ", "5551234")]
    [InlineData("+1", "")]
    [InlineData("+1", "   ")]
    public void ThrowsGivenABlankCountryCodeOrNumber(string countryCode, string number)
    {
        Should.Throw<ArgumentException>(() => PhoneNumber.Create(countryCode, number, null));
    }

    [Fact]
    public void ThrowsGivenNoCountryCodeOrNumber()
    {
        Should.Throw<ArgumentNullException>(() => PhoneNumber.Create(null!, "5551234", null));
        Should.Throw<ArgumentNullException>(() => PhoneNumber.Create("+1", null!, null));
    }

    [Fact]
    public void KeepsItsParts()
    {
        var phoneNumber = PhoneNumber.Create("+55", "11 98765 4321", "12");

        phoneNumber.CountryCode.ShouldBe("+55");
        phoneNumber.Number.ShouldBe("11 98765 4321");
        phoneNumber.Extension.ShouldBe("12");
        phoneNumber.ToString().ShouldBe("+55 11 98765 4321 x12");
    }
}
