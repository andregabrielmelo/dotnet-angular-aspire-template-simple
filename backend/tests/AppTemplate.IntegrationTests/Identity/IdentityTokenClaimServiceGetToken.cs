using System.Security.Claims;
using AppTemplate.Infrastructure.Identity;
using AppTemplate.IntegrationTests.Data;
using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AppTemplate.IntegrationTests.Identity;

/// <summary>
/// <see cref="IdentityTokenClaimService"/> signs a token that the API's own bearer scheme accepts,
/// with the session's claims and the configured lifetime.
/// </summary>
public class IdentityTokenClaimServiceGetToken(PostgresDatabaseFixture database)
    : BaseEfRepoTestFixture(database)
{
    private IIdentityService Identity => Services.GetRequiredService<IIdentityService>();

    private ITokenClaimsService Tokens => Services.GetRequiredService<ITokenClaimsService>();

    private async Task<UserId> AdminAsync()
    {
        var created = await Identity.CreateUserAsync(
            UserName.From("Ada"),
            NewEmail(),
            "correct horse battery staple",
            TestContext.Current.CancellationToken
        );
        created.IsSuccess.ShouldBeTrue();
        (
            await Identity.AddToRolesAsync(
                created.Value,
                [Role.Admin],
                TestContext.Current.CancellationToken
            )
        ).IsSuccess.ShouldBeTrue();
        return created.Value;
    }

    /// <summary>Validates the token as the API does (its JwtBearer options).</summary>
    private async Task<ClaimsIdentity> ValidateAsync(string token)
    {
        var parameters = Services
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(TokenAuthentication.BearerScheme)
            .TokenValidationParameters;
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, parameters);
        result.IsValid.ShouldBeTrue(result.Exception?.Message);
        return result.ClaimsIdentity;
    }

    [Fact]
    public async Task SignsATokenWithTheSessionsClaims()
    {
        var userId = await AdminAsync();

        var token = await Tokens.GetTokenAsync(userId, TestContext.Current.CancellationToken);

        var identity = await ValidateAsync(token.Token);
        identity.AuthenticationType.ShouldBe(TokenAuthentication.BearerScheme);
        identity
            .FindFirst(AppClaimTypes.UserId)
            ?.Value.ShouldBe(
                userId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            );
        identity
            .FindAll(AppClaimTypes.Permission)
            .Select(claim => claim.Value)
            .ShouldBe(Permission.List.Select(permission => permission.Value), ignoreOrder: true);
        identity.FindFirst(ClaimTypes.Role)?.Value.ShouldBe(Role.Admin.Name);
    }

    [Fact]
    public async Task LeavesOutTheSecurityStamp()
    {
        var userId = await AdminAsync();

        var token = await Tokens.GetTokenAsync(userId, TestContext.Current.CancellationToken);

        new JsonWebToken(token.Token).Claims.ShouldNotContain(claim =>
            claim.Type == "AspNet.Identity.SecurityStamp"
        );
    }

    [Fact]
    public async Task ExpiresAfterTheConfiguredLifetime()
    {
        var userId = await AdminAsync();
        var lifetime = Services.GetRequiredService<IOptions<JwtSettings>>().Value.TokenLifetime;

        var token = await Tokens.GetTokenAsync(userId, TestContext.Current.CancellationToken);

        var jwt = new JsonWebToken(token.Token);
        (jwt.ValidTo - jwt.IssuedAt).ShouldBe(lifetime);
        token.ExpiresAt.UtcDateTime.ShouldBe(jwt.ValidTo, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task IsRefusedWhenSignedWithAnotherKey()
    {
        var userId = await AdminAsync();
        var token = await Tokens.GetTokenAsync(userId, TestContext.Current.CancellationToken);
        var parameters = Services
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(TokenAuthentication.BearerScheme)
            .TokenValidationParameters.Clone();
        parameters.IssuerSigningKey = new SymmetricSecurityKey(new byte[32]);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Token, parameters);

        result.IsValid.ShouldBeFalse();
    }
}
