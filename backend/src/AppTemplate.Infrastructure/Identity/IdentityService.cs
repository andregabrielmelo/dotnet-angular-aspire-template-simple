using System.Text;
using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Infrastructure.Data;
using AppTemplate.UseCases.Authentication;
using AppTemplate.UseCases.Authorization;
using Ardalis.Result;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AppTemplate.Infrastructure.Identity;

/// <summary>
/// <see cref="IIdentityService"/> over ASP.NET Core Identity: <see cref="UserManager{TUser}"/> for
/// accounts, codes and passwords, and <see cref="SignInManager{TUser}"/> for password checks with
/// lockout (it issues no cookie here: the API signs in with JWTs). Codes travel Base64Url-encoded
/// in the links <see cref="AccountEmailSender"/> sends, as Identity's own API endpoints do.
/// </summary>
public sealed class IdentityService(
    ApplicationDatabaseContext _db,
    UserManager<ApplicationUser> _userManager,
    SignInManager<ApplicationUser> _signInManager,
    IEmailSender<ApplicationUser> _accountEmails,
    IOptions<AccountLinkSettings> _links,
    ILogger<IdentityService> _logger
) : IIdentityService
{
    private const string DuplicateEmail = "An account with this email already exists";
    private const string InvalidCode = "The link is invalid or has expired. Ask for a new one.";

    public Task<Result<UserId>> CreateAccountAsync(
        UserName name,
        EmailAddress email,
        string password,
        CancellationToken cancellationToken
    ) =>
        CreateAccountAsync(
            name,
            email,
            password,
            Roles.Member,
            emailConfirmed: false,
            cancellationToken
        );

    /// <summary>
    /// An account in any role, optionally already confirmed: registration uses the overload
    /// above; seed data and tests use this one.
    /// </summary>
    public async Task<Result<UserId>> CreateAccountAsync(
        UserName name,
        EmailAddress email,
        string password,
        string role,
        bool emailConfirmed,
        CancellationToken cancellationToken
    )
    {
        // The profile first, for its id; the account then points at it. One transaction, so a
        // failure leaves neither behind.
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
                return Abandon(ToResult(created, passwordField: "password"));
            }

            var inRole = await _userManager.AddToRoleAsync(account, role);
            if (!inRole.Succeeded)
            {
                throw new InvalidOperationException(
                    $"The new account could not join the '{role}' role: {Describe(inRole)}"
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
            return Abandon(Result.Conflict(DuplicateEmail));
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
            // Hash anyway, so an unknown email takes as long as a wrong password.
            _userManager.PasswordHasher.HashPassword(new ApplicationUser(), password);
            return Result.Unauthorized();
        }

        var signIn = await _signInManager.CheckPasswordSignInAsync(
            account,
            password,
            lockoutOnFailure: true
        );
        if (signIn.Succeeded)
        {
            return await ToAccountDtoAsync(account);
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

    public async Task<AccountDto?> GetAccountAsync(
        UserId userId,
        CancellationToken cancellationToken
    )
    {
        var account = await FindAsync(userId, cancellationToken);
        return account is null ? null : await ToAccountDtoAsync(account);
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
        if (account is null || Decode(code) is not { } token)
        {
            return InvalidCodeResult();
        }

        var confirmed = await _userManager.ConfirmEmailAsync(account, token);
        return confirmed.Succeeded ? Result.Success() : InvalidCodeResult();
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

        var code = Encode(await _userManager.GeneratePasswordResetTokenAsync(account));
        var link = Link(_links.Value.ResetPasswordPath, ("email", account.Email!), ("code", code));
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
        if (account is null || Decode(code) is not { } token)
        {
            return InvalidCodeResult();
        }

        var reset = await _userManager.ResetPasswordAsync(account, token, newPassword);
        if (!reset.Succeeded)
        {
            return ToResult(reset, passwordField: "newPassword");
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
        return ToResult(changed, passwordField: "newPassword");
    }

    private Task<ApplicationUser?> FindAsync(UserId userId, CancellationToken cancellationToken) =>
        _userManager.Users.SingleOrDefaultAsync(
            account => account.ProfileId == userId,
            cancellationToken
        );

    private async Task SendEmailConfirmationAsync(ApplicationUser? account)
    {
        if (account is null || account.EmailConfirmed)
        {
            return;
        }

        var code = Encode(await _userManager.GenerateEmailConfirmationTokenAsync(account));
        var link = Link(
            _links.Value.ConfirmEmailPath,
            ("userId", account.ProfileId.Value.ToString()),
            ("code", code)
        );
        await _accountEmails.SendConfirmationLinkAsync(account, account.Email!, link);
    }

    private async Task<AccountDto> ToAccountDtoAsync(ApplicationUser account)
    {
        var roles = await _userManager.GetRolesAsync(account);
        return new AccountDto(
            account.ProfileId,
            new EmailAddress(account.Email!),
            account.EmailConfirmed,
            roles.Order(StringComparer.Ordinal).ToList(),
            account.SecurityStamp!,
            await _userManager.IsLockedOutAsync(account)
        );
    }

    /// <summary>Drops what a failed creation left tracked; the transaction rolls back on dispose.</summary>
    private Result<UserId> Abandon(Result result)
    {
        _db.ChangeTracker.Clear();
        return result;
    }

    private string Link(string path, params (string Name, string Value)[] query)
    {
        var url = _links.Value.ClientUrl.TrimEnd('/') + path;
        return url
            + "?"
            + string.Join(
                "&",
                query.Select(part => $"{part.Name}={Uri.EscapeDataString(part.Value)}")
            );
    }

    private static string Encode(string token) =>
        WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    private static string? Decode(string code)
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

    private static Result InvalidCodeResult() =>
        Result.Invalid(new ValidationError { Identifier = "code", ErrorMessage = InvalidCode });

    /// <summary>
    /// Identity's errors as the API's results: duplicates are <c>Conflict</c>, the rest are field
    /// errors keyed by the request field they belong to.
    /// </summary>
    private Result ToResult(IdentityResult result, string passwordField)
    {
        if (result.Succeeded)
        {
            return Result.Success();
        }

        var describer = new IdentityErrorDescriber();
        if (
            result.Errors.Any(error =>
                error.Code
                    is nameof(describer.DuplicateEmail)
                        or nameof(describer.DuplicateUserName)
            )
        )
        {
            return Result.Conflict(DuplicateEmail);
        }

        var fieldErrors = result
            .Errors.Select(error =>
                (
                    Field: error.Code switch
                    {
                        nameof(describer.PasswordMismatch) => "currentPassword",
                        nameof(describer.InvalidToken) => "code",
                        nameof(describer.InvalidEmail) or nameof(describer.InvalidUserName) =>
                            "email",
                        _ when error.Code.StartsWith("Password", StringComparison.Ordinal) =>
                            passwordField,
                        _ => null,
                    },
                    error.Description
                )
            )
            .ToList();

        if (fieldErrors.Any(error => error.Field is null))
        {
            _logger.LogError("Identity refused an account change: {Errors}", Describe(result));
            return Result.Error();
        }

        return Result.Invalid(
            fieldErrors
                .Select(error => new ValidationError
                {
                    Identifier = error.Field!,
                    ErrorMessage = error.Field == "code" ? InvalidCode : error.Description,
                })
                .ToList()
        );
    }

    // Error codes only: descriptions can quote the input.
    private static string Describe(IdentityResult result) =>
        string.Join(", ", result.Errors.Select(error => error.Code));
}
