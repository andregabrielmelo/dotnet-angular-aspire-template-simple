namespace AppTemplate.Infrastructure.Identity;

/// <summary>
/// Where account emails link to: the web app's pages that confirm an email and reset a password.
/// Bound from <c>AccountLinks</c>; <see cref="ClientUrl"/> is required.
/// </summary>
public class AccountLinkSettings
{
    public const string SectionName = "AccountLinks";

    /// <summary>The web app's origin, such as <c>https://app.example.com</c>.</summary>
    public string ClientUrl { get; set; } = string.Empty;

    public string ConfirmEmailPath { get; set; } = "/auth/confirm-email";

    public string ResetPasswordPath { get; set; } = "/auth/reset-password";
}
