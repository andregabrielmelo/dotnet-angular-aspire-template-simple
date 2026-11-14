using System.Text.Json.Serialization;
using Ardalis.GuardClauses;

namespace AppTemplate.Core.ValueObjects;

/// <summary>
/// A value object representing a phone number: a country code, the number, and an optional
/// extension. Create one with <see cref="Create"/>, which refuses a blank country code or number.
/// </summary>
public class PhoneNumber : ValueObject
{
    public string CountryCode { get; private set; }
    public string Number { get; private set; }
    public string? Extension { get; private set; }
    public static PhoneNumber Unknown { get; } = new PhoneNumber(String.Empty, String.Empty, null);

    // Private: callers go through Create. EF Core binds to it by its parameter names when it
    // loads the owned type, and System.Text.Json through [JsonConstructor] when HybridCache reads
    // a cached result back (CachedResultRoundTrip).
    [JsonConstructor]
    private PhoneNumber(string countryCode, string number, string? extension)
    {
        CountryCode = countryCode;
        Number = number;
        Extension = extension;
    }

    public static PhoneNumber Create(string countryCode, string number, string? extension)
    {
        Guard.Against.NullOrWhiteSpace(countryCode, nameof(countryCode));
        Guard.Against.NullOrWhiteSpace(number, nameof(number));

        return new PhoneNumber(countryCode, number, extension);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return CountryCode;
        yield return Number;
        yield return Extension ?? String.Empty;
    }

    public override string ToString()
    {
        return $"{CountryCode} {Number}{(string.IsNullOrEmpty(Extension) ? String.Empty : $" x{Extension}")}";
    }
}
