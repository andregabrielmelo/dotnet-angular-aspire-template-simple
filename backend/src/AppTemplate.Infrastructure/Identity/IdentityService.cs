using System.Globalization;
using System.Text;
using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;
using AppTemplate.Infrastructure.Data;
using AppTemplate.UseCases.Authentication;
using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Interfaces;
using Ardalis.GuardClauses;
using Ardalis.Result;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AppTemplate.Infrastructure.Identity;

/// <summary>
/// <see cref="IIdentityService"/> over ASP.NET Core Identity: <see cref="UserManager{TUser}"/> for
/// accounts, codes, passwords and roles, and <see cref="SignInManager{TUser}"/> for password checks
/// with lockout and for the session cookie (ADR 015). Codes travel
/// Base64Url-encoded in the links <see cref="AccountEmailSender"/> sends, as Identity's own API
/// endpoints do.
/// </summary>
public sealed class IdentityService(
    ApplicationDatabaseContext db,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IEmailSender<ApplicationUser> accountEmails,
    IOptions<AccountLinkSettings> links
) : IIdentityService
{
    private readonly ApplicationDatabaseContext _db = db;
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly SignInManager<ApplicationUser> _signInManager = signInManager;
    private readonly IEmailSender<ApplicationUser> _accountEmails = accountEmails;
    private readonly IOptions<AccountLinkSettings> _links = links;

    public async Task<Result<UserId>> CreateUserAsync(
        UserName name,
        EmailAddress email,
        string password,
        CancellationToken cancellationToken
    )
    {
        // The account carries its profile: Identity validates it first (a taken email, the
        // password rules), then its store adds both and saves them in one SaveChanges, so a
        // refused account leaves no profile behind.
        var profile = User.Create(name);
        var account = new ApplicationUser
        {
            ProfileId = profile.Id,
            Profile = profile,
            // The email is the user name, as Identity's own endpoints do; its unique index
            // stops two concurrent registrations with one address.
            UserName = email.Value,
            Email = email.Value,
        };

        try
        {
            var created = await _userManager.CreateAsync(account, password);
            return created.Succeeded
                ? profile.Id
                : created.ToApplicationResult(passwordField: "password");
        }
        catch (DbUpdateException exception)
            when (exception.InnerException
                    is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }
            )
        {
            // Another registration with this email won the race. Nothing was saved; stop
            // tracking both, so a later save in this scope doesn't try them again.
            _db.Entry(account).State = EntityState.Detached;
            _db.Entry(profile).State = EntityState.Detached;
            return UserErrors.EmailNotUnique;
        }
    }

    public async Task<Result> AddToRolesAsync(
        UserId userId,
        IEnumerable<Role> roles,
        CancellationToken cancellationToken
    )
    {
        var account = await _userManager.Users.SingleOrDefaultAsync(
            candidate => candidate.ProfileId == userId,
            cancellationToken
        );
        if (account is null)
        {
            return Result.NotFound();
        }

        var current = await _userManager.GetRolesAsync(account);
        var missing = roles.Select(role => role.Name).Distinct().Except(current).ToList();
        if (missing.Count == 0)
        {
            return Result.Success();
        }

        var added = await _userManager.AddToRolesAsync(account, missing);
        return added.ToApplicationResult();
    }

    public async Task<Result<AccountDto>> CheckPasswordAsync(
        EmailAddress email,
        string password,
        CancellationToken cancellationToken
    )
    {
        var account = await _userManager.FindByEmailAsync(email.Value);
        if (account is null)
        {
            // Hash the attempt anyway, so an unknown email takes as long as a wrong password
            // and the timing doesn't reveal which emails have accounts.
            HashForTiming(password);
            return Result.Unauthorized();
        }

        var signIn = await _signInManager.CheckPasswordSignInAsync(
            account,
            password,
            lockoutOnFailure: true
        );
        if (signIn.IsLockedOut)
        {
            // Identity answers a locked-out account without hashing; hash anyway, so a locked-out
            // account doesn't answer faster than an unknown email.
            HashForTiming(password);
        }

        if (signIn.Succeeded)
        {
            var roles = await _userManager.GetRolesAsync(account);
            return new AccountDto(
                account.ProfileId,
                new EmailAddress(account.Email!),
                account.EmailConfirmed,
                roles.Order(StringComparer.Ordinal).ToList()
            );
        }

        if (signIn.IsNotAllowed)
        {
            // Identity says "not allowed" (unconfirmed) before it checks the password. Say so
            // only to someone who knows the password, and count a wrong one toward lockout.
            if (await _userManager.IsLockedOutAsync(account))
            {
                return Result.Unauthorized();
            }

            if (!await _userManager.CheckPasswordAsync(account, password))
            {
                await _userManager.AccessFailedAsync(account);
                return Result.Unauthorized();
            }

            return Result.Forbidden("Confirm your email address before signing in.");
        }

        // A wrong password or a locked-out account: the same answer for both.
        return Result.Unauthorized();
    }

    public async Task SignInAsync(UserId userId, CancellationToken cancellationToken)
    {
        var account = await _userManager.Users.SingleOrDefaultAsync(
            candidate => candidate.ProfileId == userId,
            cancellationToken
        );
        await _signInManager.SignInAsync(
            Guard.Against.NotFound(userId, account),
            isPersistent: true
        );
    }

    public async Task RefreshSignInAsync(UserId userId, CancellationToken cancellationToken)
    {
        var account = await _userManager.Users.SingleOrDefaultAsync(
            candidate => candidate.ProfileId == userId,
            cancellationToken
        );
        await _signInManager.RefreshSignInAsync(Guard.Against.NotFound(userId, account));
    }

    public Task SignOutAsync() => _signInManager.SignOutAsync();

    public async Task<AccountDto?> GetUserAsync(UserId userId, CancellationToken cancellationToken)
    {
        var account = await _userManager.Users.SingleOrDefaultAsync(
            candidate => candidate.ProfileId == userId,
            cancellationToken
        );
        if (account is null)
        {
            return null;
        }

        var roles = await _userManager.GetRolesAsync(account);
        return new AccountDto(
            account.ProfileId,
            new EmailAddress(account.Email!),
            account.EmailConfirmed,
            roles.Order(StringComparer.Ordinal).ToList()
        );
    }

    public async Task SendEmailConfirmationAsync(UserId userId, CancellationToken cancellationToken)
    {
        var account = await _userManager.Users.SingleOrDefaultAsync(
            candidate => candidate.ProfileId == userId,
            cancellationToken
        );
        await SendEmailConfirmationAsync(account);
    }

    // TODO: I think it should only work by passing userId instead of e-mail
    public async Task SendEmailConfirmationAsync(
        EmailAddress email,
        CancellationToken cancellationToken
    ) => await SendEmailConfirmationAsync(await _userManager.FindByEmailAsync(email.Value));

    public async Task<Result> ConfirmEmailAsync(
        UserId userId,
        string code,
        CancellationToken cancellationToken
    )
    {
        var account = await _userManager.Users.SingleOrDefaultAsync(
            candidate => candidate.ProfileId == userId,
            cancellationToken
        );
        if (account is null || TryDecodeCode(code) is not { } token)
        {
            return IdentityErrors.InvalidCodeResult;
        }

        var confirmed = await _userManager.ConfirmEmailAsync(account, token);
        return confirmed.Succeeded ? Result.Success() : IdentityErrors.InvalidCodeResult;
    }

    public async Task SendPasswordResetAsync(
        EmailAddress email,
        CancellationToken cancellationToken
    )
    {
        var account = await _userManager.FindByEmailAsync(email.Value);
        if (account is null || !account.EmailConfirmed)
        {
            return;
        }

        var passwordResetToken = await _userManager.GeneratePasswordResetTokenAsync(account);
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(passwordResetToken));

        var link = QueryHelpers.AddQueryString(
            $"{_links.Value.ClientUrl.TrimEnd('/')}{_links.Value.ResetPasswordPath}",
            new Dictionary<string, string?> { ["email"] = account.Email, ["code"] = code }
        );

        await _accountEmails.SendPasswordResetLinkAsync(account, account.Email!, link);
    }

    public async Task<Result> ResetPasswordAsync(
        EmailAddress email,
        string code,
        string newPassword,
        CancellationToken cancellationToken
    )
    {
        var account = await _userManager.FindByEmailAsync(email.Value);
        if (account is null || TryDecodeCode(code) is not { } token)
        {
            return IdentityErrors.InvalidCodeResult;
        }

        var reset = await _userManager.ResetPasswordAsync(account, token, newPassword);
        if (!reset.Succeeded)
        {
            return reset.ToApplicationResult(passwordField: "newPassword");
        }

        // Whoever can read the account's email may sign in again at once.
        await _userManager.ResetAccessFailedCountAsync(account);
        await _userManager.SetLockoutEndDateAsync(account, null);
        return Result.Success();
    }

    public async Task<Result> ChangePasswordAsync(
        UserId userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken
    )
    {
        var account = await _userManager.Users.SingleOrDefaultAsync(
            candidate => candidate.ProfileId == userId,
            cancellationToken
        );
        if (account is null)
        {
            return Result.NotFound();
        }

        var changed = await _userManager.ChangePasswordAsync(account, currentPassword, newPassword);
        return changed.ToApplicationResult(passwordField: "newPassword");
    }

    // An account and a hash to verify against when the email is unknown (CheckPasswordAsync). The
    // hash is made once, with the configured hasher, so verifying it costs what a real one does.
    private static readonly ApplicationUser TimingAccount = new() { ProfileId = UserId.From(1) };
    private static string? s_timingHash;

    private void HashForTiming(string password)
    {
        s_timingHash ??= _userManager.PasswordHasher.HashPassword(
            TimingAccount,
            Guid.NewGuid().ToString()
        );
        _userManager.PasswordHasher.VerifyHashedPassword(TimingAccount, s_timingHash, password);
    }

    /// <summary>
    /// The Identity token inside an emailed code, or <c>null</c> when the code isn't valid
    /// Base64Url (a mangled or hand-edited link), which callers answer as an invalid code.
    /// </summary>
    private static string? TryDecodeCode(string code)
    {
        try
        {
            return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private async Task SendEmailConfirmationAsync(ApplicationUser? account)
    {
        if (account is null || account.EmailConfirmed)
        {
            return;
        }

        var emailConfirmationToken = await _userManager.GenerateEmailConfirmationTokenAsync(
            account
        );
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(emailConfirmationToken));

        var link = QueryHelpers.AddQueryString(
            $"{_links.Value.ClientUrl.TrimEnd('/')}{_links.Value.ConfirmEmailPath}",
            new Dictionary<string, string?>
            {
                ["userId"] = account.ProfileId.Value.ToString(CultureInfo.InvariantCulture),
                ["code"] = code,
            }
        );

        await _accountEmails.SendConfirmationLinkAsync(account, account.Email!, link);
    }
}
