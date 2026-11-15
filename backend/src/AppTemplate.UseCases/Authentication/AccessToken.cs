namespace AppTemplate.UseCases.Authentication;

/// <summary>A signed bearer token and when it stops being accepted (ADR 018).</summary>
public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);
