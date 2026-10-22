using Microsoft.Extensions.Compliance.Classification;

namespace AppTemplate.SharedKernel;

/// <summary>
/// The data classes logs must never contain in clear text. Mark a type (such as
/// <c>EmailAddress</c>) or a property (<c>[property: PersonalData]</c> on a record parameter)
/// and the logging pipeline redacts it wherever it's logged, including inside a destructured
/// <c>{@Object}</c>. See docs/content/best-practices.md, Logging.
/// </summary>
public static class AppDataTaxonomy
{
    public const string TaxonomyName = "AppTemplate";

    /// <summary>Data about an identifiable person: email, phone number, address.</summary>
    public static DataClassification PersonalData { get; } =
        new(TaxonomyName, nameof(PersonalData));

    /// <summary>Credentials: passwords, tokens, API keys, client secrets.</summary>
    public static DataClassification SecretData { get; } = new(TaxonomyName, nameof(SecretData));
}

[AttributeUsage(
    AttributeTargets.Class
        | AttributeTargets.Struct
        | AttributeTargets.Property
        | AttributeTargets.Field
        | AttributeTargets.Parameter
)]
public sealed class PersonalDataAttribute()
    : DataClassificationAttribute(AppDataTaxonomy.PersonalData);

[AttributeUsage(
    AttributeTargets.Class
        | AttributeTargets.Struct
        | AttributeTargets.Property
        | AttributeTargets.Field
        | AttributeTargets.Parameter
)]
public sealed class SecretDataAttribute() : DataClassificationAttribute(AppDataTaxonomy.SecretData);
