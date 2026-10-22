using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;
using AppTemplate.UseCases.Caching;
using Microsoft.Extensions.Caching.Hybrid;

namespace AppTemplate.UseCases.Users;

/// <summary>
/// What gets stored in HybridCache for a user: plain primitives, so it serializes cleanly to
/// the distributed (L2) cache with System.Text.Json and never exposes the tracked entity.
/// Domain types are rebuilt on the way out.
/// </summary>
public sealed record CachedUser(
    int Id,
    string ExternalId,
    string Name,
    string Email,
    string? PhoneCountryCode,
    string? PhoneLocalNumber,
    string? PhoneExtension
)
{
    /// <summary>
    /// A short local (L1) lifetime bounds how long another instance can serve a stale copy
    /// after an invalidation; the longer L2 lifetime keeps most reads off the database.
    /// </summary>
    public static readonly HybridCacheEntryOptions EntryOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(10),
        LocalCacheExpiration = TimeSpan.FromMinutes(1),
    };

    public static readonly string[] Tags = [CacheTags.Users];

    public static string KeyById(UserId id) => $"users:id:{id.Value}";

    public static string KeyByExternalId(string externalId) => $"users:external:{externalId}";

    public static CachedUser FromEntity(User user) =>
        new(
            user.Id.Value,
            user.ExternalId,
            user.Name.Value,
            user.Email.Value,
            user.PhoneNumber?.CountryCode,
            user.PhoneNumber?.Number,
            user.PhoneNumber?.Extension
        );

    public UserDto ToUserDto() =>
        new(
            UserId.From(Id),
            UserName.From(Name),
            PhoneLocalNumber is null
                ? PhoneNumber.Unknown
                : new PhoneNumber(
                    PhoneCountryCode ?? string.Empty,
                    PhoneLocalNumber,
                    PhoneExtension
                )
        );
}
