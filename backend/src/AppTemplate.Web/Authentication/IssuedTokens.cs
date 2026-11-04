using System.Text.Json;
using System.Text.Json.Serialization;

namespace AppTemplate.Web.Authentication;

/// <summary>
/// The token pair <see cref="UserTokenService"/> creates, like the FastEndpoints demo's
/// <c>MyTokenResponse</c>. FastEndpoints' own refresh endpoint sends it as is, so it serializes as
/// <see cref="AccessTokenResponse"/>: the refresh token only travels in its HttpOnly cookie.
/// </summary>
[JsonConverter(typeof(AccessTokenOnlyConverter))]
public sealed class IssuedTokens : TokenResponse
{
    private sealed class AccessTokenOnlyConverter : JsonConverter<IssuedTokens>
    {
        public override IssuedTokens Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options
        ) => throw new NotSupportedException("Issued tokens are only ever written.");

        public override void Write(
            Utf8JsonWriter writer,
            IssuedTokens value,
            JsonSerializerOptions options
        ) => JsonSerializer.Serialize(writer, AccessTokenResponse.From(value), options);
    }
}
