using System.Text.RegularExpressions;
using FluentValidation.Validators;

namespace AppTemplate.Web.Validation;

/// <summary>
/// Phone rules in the style of FluentValidation's built-ins (<c>EmailAddress()</c> and the
/// like): <c>RuleFor(x => x.PhoneNumber).PhoneNumber()</c>. Like those, null or empty values
/// pass - chain <c>NotEmpty()</c> when the value is required.
/// </summary>
public static class PhoneValidatorExtensions
{
    /// <summary>A country calling code: '+' and 1 to 3 digits, e.g. "+1" or "+55".</summary>
    public static IRuleBuilderOptions<T, string?> PhoneCountryCode<T>(
        this IRuleBuilder<T, string?> ruleBuilder
    ) => ruleBuilder.SetValidator(new PhoneCountryCodeValidator<T>());

    /// <summary>A local phone number: 4 to 20 digits, spaces, '(', ')' or '-'.</summary>
    public static IRuleBuilderOptions<T, string?> PhoneNumber<T>(
        this IRuleBuilder<T, string?> ruleBuilder
    ) => ruleBuilder.SetValidator(new PhoneNumberValidator<T>());

    /// <summary>A phone extension: 1 to 10 digits.</summary>
    public static IRuleBuilderOptions<T, string?> PhoneExtension<T>(
        this IRuleBuilder<T, string?> ruleBuilder
    ) => ruleBuilder.SetValidator(new PhoneExtensionValidator<T>());
}

public sealed partial class PhoneCountryCodeValidator<T> : PropertyValidator<T, string?>
{
    public override string Name => nameof(PhoneCountryCodeValidator<>);

    public override bool IsValid(FluentValidation.ValidationContext<T> context, string? value) =>
        string.IsNullOrEmpty(value) || Pattern().IsMatch(value);

    protected override string GetDefaultMessageTemplate(string errorCode) =>
        "'{PropertyName}' must be a country calling code such as +1 or +55.";

    [GeneratedRegex(@"^\+[1-9]\d{0,2}$")]
    private static partial Regex Pattern();
}

public sealed partial class PhoneNumberValidator<T> : PropertyValidator<T, string?>
{
    public override string Name => nameof(PhoneNumberValidator<>);

    public override bool IsValid(FluentValidation.ValidationContext<T> context, string? value) =>
        string.IsNullOrEmpty(value) || Pattern().IsMatch(value);

    protected override string GetDefaultMessageTemplate(string errorCode) =>
        "'{PropertyName}' must be 4 to 20 characters of digits, spaces, '(', ')' or '-'.";

    [GeneratedRegex(@"^[0-9 ()-]{4,20}$")]
    private static partial Regex Pattern();
}

public sealed partial class PhoneExtensionValidator<T> : PropertyValidator<T, string?>
{
    public override string Name => nameof(PhoneExtensionValidator<>);

    public override bool IsValid(FluentValidation.ValidationContext<T> context, string? value) =>
        string.IsNullOrEmpty(value) || Pattern().IsMatch(value);

    protected override string GetDefaultMessageTemplate(string errorCode) =>
        "'{PropertyName}' must be 1 to 10 digits.";

    [GeneratedRegex(@"^\d{1,10}$")]
    private static partial Regex Pattern();
}
