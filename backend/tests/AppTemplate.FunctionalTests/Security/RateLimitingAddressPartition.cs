using AppTemplate.Web.Configurations;

namespace AppTemplate.FunctionalTests.Security;

/// <summary>
/// The address part of an anonymous caller's rate limit partition. Pure, so no host or database.
/// </summary>
public class RateLimitingAddressPartition
{
    [Theory]
    [InlineData("203.0.113.7", "203.0.113.7")]
    [InlineData("127.0.0.1", "127.0.0.1")]
    [InlineData("::ffff:203.0.113.7", "203.0.113.7")]
    [InlineData("2001:db8:1:2:3:4:5:6", "2001:db8:1:2::/64")]
    [InlineData("2001:db8:1:2::", "2001:db8:1:2::/64")]
    [InlineData("::1", "::/64")]
    public void CountsIPv4ByAddressAndIPv6ByPrefix(string address, string expected) =>
        RateLimitingConfigurations.AddressPartition(IPAddress.Parse(address)).ShouldBe(expected);

    [Fact]
    public void PutsAddressesInOneIPv6PrefixTogether() =>
        RateLimitingConfigurations
            .AddressPartition(IPAddress.Parse("2001:db8:aa:bb:1::1"))
            .ShouldBe(
                RateLimitingConfigurations.AddressPartition(
                    IPAddress.Parse("2001:db8:aa:bb:ffff:ffff:ffff:ffff")
                )
            );

    [Fact]
    public void SeparatesNeighbouringIPv6Prefixes() =>
        RateLimitingConfigurations
            .AddressPartition(IPAddress.Parse("2001:db8:aa:bb::1"))
            .ShouldNotBe(
                RateLimitingConfigurations.AddressPartition(IPAddress.Parse("2001:db8:aa:bc::1"))
            );

    [Fact]
    public void NamesAMissingAddressUnknown() =>
        RateLimitingConfigurations.AddressPartition(null).ShouldBe("unknown");
}
