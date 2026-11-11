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
using Microsoft.Extensions.Logging;
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
    ApplicationDatabaseContext _db,
    UserManager<ApplicationUser> _userManager,
    SignInManager<ApplicationUser> _signInManager,
    IEmailSender<ApplicationUser> _accountEmails,
    IOptions<AccountLinkSettings> _links
) : IIdentityService
{
    public Task<Result<UserId>> CreateUserAsync(
        UserName name,
        EmailAddress email,
        string password,
        CancellationToken cancellationToken
    ) =>
        CreateUserAsync(
            name,
            email,
            password,
            Role.Member,
            emailConfirmed: false,
            cancellationToken
        );

    public Task<Result<UserId>> CreateUserAsync(
        UserName name,
        EmailAddress email,
        string password,
        Role role,
        bool emailConfirmed,
        CancellationToken cancellationToken
    ) =>
        // The transaction runs inside the execution strategy: Aspire's EF integration retries
        // transient failures, and a retrying strategy only accepts a transaction it can replay
        // as a whole.
        _db
            .Database.CreateExecutionStrategy()
            .ExecuteAsync(
                attemptCancellation =>
                    CreateInTransactionAsync(
                        name,
                        email,
                        password,
                        role,
                        emailConfirmed,
                        attemptCancellation
                    ),
                cancellationToken
            );

    /// <summary>
    /// The profile first, for its id; the account then points at it. One transaction, so a
    /// failure leaves neither behind. Starts from a clean change tracker, so a retried attempt
    /// doesn't carry entities from the failed one.
    /// </summary>
    private async Task<Result<UserId>> CreateInTransactionAsync(
        UserName name,
        EmailAddress email,
        string password,
        Role role,
        bool emailConfirmed,
        CancellationToken cancellationToken
    )
    {
        _db.ChangeTracker.Clear();
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var profile = User.Create(name);
            _db.UserProfiles.Add(profile);
            await _db.SaveChangesAsync(cancellationToken);

            var account = new ApplicationUser
            {
                ProfileId = profile.Id,
                // The email is the user name, as Identity's own endpoints do; its unique index
                // stops two concurrent registrations with one address.
                UserName = email.Value,
                Email = email.Value,
                EmailConfirmed = emailConfirmed,
            };
            var created = await _userManager.CreateAsync(account, password);
            if (!created.Succeeded)
            {
                return Abandon(created.ToApplicationResult());
            }

            var inRole = await _userManager.AddToRoleAsync(account, role.Name);
            if (!inRole.Succeeded)
            {
                throw new InvalidOperationException(
                    $"The new account could not join the '{role.Name}' role: {string.Join(", ", inRole.Errors.Select(error => error.Code))}"
                );
            }

            await transaction.CommitAsync(cancellationToken);
            return profile.Id;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException
                    is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }
            )
        {
            // Another registration with this email won the race.
            return Abandon(UserErrors.EmailNotUnique);
        }
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
            return Result.Unauthorized();
        }

        var signIn = await _signInManager.CheckPasswordSignInAsync(
            account,
            password,
            lockoutOnFailure: true
        );
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

    public async Task SignInAsync(UserId userId, CancellationToken cancellationToken) =>
        await _signInManager.SignInAsync(
            await FindRequiredAsync(userId, cancellationToken),
            isPersistent: true
        );

    public async Task RefreshSignInAsync(UserId userId, CancellationToken cancellationToken) =>
        await _signInManager.RefreshSignInAsync(await FindRequiredAsync(userId, cancellationToken));

    public Task SignOutAsync() => _signInManager.SignOutAsync();

    public async Task<AccountDto?> GetUserAsync(UserId userId, CancellationToken cancellationToken)
    {
        var account = await FindAsync(userId, cancellationToken);
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

    public async Task SendEmailConfirmationAsync(
        UserId userId,
        CancellationToken cancellationToken
    ) => await SendEmailConfirmationAsync(await FindAsync(userId, cancellationToken));

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
        var account = await FindAsync(userId, cancellationToken);
        if (
            account is null
            || Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code)) is not { } token
        )
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
        if (
            account is null
            || Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code)) is not { } token
        )
        {
            return IdentityErrors.InvalidCodeResult;
        }

        var reset = await _userManager.ResetPasswordAsync(account, token, newPassword);
        if (!reset.Succeeded)
        {
            return reset.ToApplicationResult();
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
        var account = await FindAsync(userId, cancellationToken);
        if (account is null)
        {
            return Result.NotFound();
        }

        var changed = await _userManager.ChangePasswordAsync(account, currentPassword, newPassword);
        return changed.ToApplicationResult();
    }

    private Task<ApplicationUser?> FindAsync(UserId userId, CancellationToken cancellationToken) =>
        _userManager.Users.SingleOrDefaultAsync(
            account => account.ProfileId == userId,
            cancellationToken
        );

    private async Task<ApplicationUser> FindRequiredAsync(
        UserId userId,
        CancellationToken cancellationToken
    ) => Guard.Against.NotFound(userId, await FindAsync(userId, cancellationToken));

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
                ["userId"] = account.ProfileId.Value.ToString(),
                ["code"] = code,
            }
        );

        await _accountEmails.SendConfirmationLinkAsync(account, account.Email!, link);
    }

    /// <summary>Drops what a failed creation left tracked; the transaction rolls back on dispose.</summary>
    private Result<UserId> Abandon(Result result)
    {
        _db.ChangeTracker.Clear();
        return result;
    }
}
