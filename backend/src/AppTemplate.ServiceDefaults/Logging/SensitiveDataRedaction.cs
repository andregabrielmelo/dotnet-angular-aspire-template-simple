using System.Collections.Concurrent;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;
using Serilog.Core;
using Serilog.Events;

namespace AppTemplate.ServiceDefaults.Logging;

/// <summary>Replaces any classified value with a fixed marker, so a reader sees something was there.</summary>
public sealed class PlaceholderRedactor : Redactor
{
    public const string Placeholder = "[redacted]";

    public override int GetRedactedLength(ReadOnlySpan<char> input) => Placeholder.Length;

    public override int Redact(ReadOnlySpan<char> source, Span<char> destination)
    {
        Placeholder.AsSpan().CopyTo(destination);
        return Placeholder.Length;
    }
}

/// <summary>
/// Runs when an object is destructured (<c>{@Request}</c>). A value whose type carries a
/// <see cref="DataClassificationAttribute"/> (such as <c>EmailAddress</c>) becomes the
/// redactor's output; an object with classified properties is destructured with those
/// properties redacted and the rest as usual.
/// </summary>
public sealed class ClassifiedDataDestructuringPolicy(IRedactorProvider redactors)
    : Serilog.Core.IDestructuringPolicy
{
    private static readonly ConcurrentDictionary<Type, ClassifiedShape> Shapes = new();

    public bool TryDestructure(
        object value,
        ILogEventPropertyValueFactory propertyValueFactory,
        out LogEventPropertyValue result
    )
    {
        var shape = Shapes.GetOrAdd(value.GetType(), ClassifiedShape.Of);

        if (shape.TypeClassification is { } typeClassification)
        {
            result = new ScalarValue(Redact(typeClassification, value.ToString()));
            return true;
        }

        if (shape.Properties is null)
        {
            result = null!;
            return false;
        }

        var properties = new List<LogEventProperty>(shape.Properties.Length);
        foreach (var (property, classification) in shape.Properties)
        {
            var propertyValue = property.GetValue(value);
            properties.Add(
                new LogEventProperty(
                    property.Name,
                    classification is null
                        ? propertyValueFactory.CreatePropertyValue(propertyValue, true)
                        : new ScalarValue(
                            propertyValue is null
                                ? null
                                : Redact(classification.Value, propertyValue.ToString())
                        )
                )
            );
        }
        result = new StructureValue(properties, value.GetType().Name);
        return true;
    }

    private string Redact(DataClassification classification, string? value) =>
        redactors.GetRedactor(new DataClassificationSet(classification)).Redact(value);

    /// <summary>What reflection found about a type, computed once per type.</summary>
    private sealed record ClassifiedShape(
        DataClassification? TypeClassification,
        (PropertyInfo Property, DataClassification? Classification)[]? Properties
    )
    {
        public static ClassifiedShape Of(Type type)
        {
            if (Classification(type) is { } typeClassification)
            {
                return new ClassifiedShape(typeClassification, null);
            }

            if (type.IsPrimitive || type == typeof(string) || type.IsEnum)
            {
                return new ClassifiedShape(null, null);
            }

            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                .Select(p => (p, Classification(p) ?? Classification(p.PropertyType)))
                .ToArray();

            // Only take over objects that have something to hide; Serilog handles the rest.
            return properties.Any(p => p.Item2 is not null)
                ? new ClassifiedShape(null, properties)
                : new ClassifiedShape(null, null);
        }

        private static DataClassification? Classification(MemberInfo member) =>
            member.GetCustomAttribute<DataClassificationAttribute>(inherit: true)?.Classification;
    }
}

/// <summary>
/// The last step before any sink, for values that carry no classification: a string property
/// whose name says what it is (<c>Email</c>, <c>PhoneNumber</c>, <c>AccessToken</c>, ...) is
/// replaced, and any other string has emails, JWTs and bearer tokens masked. It also catches a
/// classified object logged without <c>@</c>, which Serilog captures as-is.
/// </summary>
public sealed partial class SensitiveDataEnricher : ILogEventEnricher
{
    private static readonly string[] SensitiveNameParts =
    [
        "email",
        "phone",
        "password",
        "passwd",
        "secret",
        "token",
        "authorization",
        "cookie",
        "apikey",
        "api_key",
        "credential",
    ];

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var (name, value) in logEvent.Properties.ToArray())
        {
            var redacted = Redact(name, value);
            if (!ReferenceEquals(redacted, value))
            {
                logEvent.AddOrUpdateProperty(new LogEventProperty(name, redacted));
            }
        }
    }

    public static bool IsSensitiveName(string name) =>
        SensitiveNameParts.Any(part => name.Contains(part, StringComparison.OrdinalIgnoreCase));

    /// <summary>Returns <paramref name="value"/> itself when nothing in it needed redacting.</summary>
    private static LogEventPropertyValue Redact(string name, LogEventPropertyValue value)
    {
        switch (value)
        {
            case ScalarValue { Value: string text }:
                if (IsSensitiveName(name))
                {
                    return new ScalarValue(PlaceholderRedactor.Placeholder);
                }
                var masked = MaskPatterns(text);
                return ReferenceEquals(masked, text) ? value : new ScalarValue(masked);

            case ScalarValue { Value: { } other }
                when other.GetType().GetCustomAttribute<DataClassificationAttribute>(true)
                    is not null:
                return new ScalarValue(PlaceholderRedactor.Placeholder);

            case StructureValue structure:
            {
                var changed = false;
                var properties = structure
                    .Properties.Select(p =>
                    {
                        var redacted = Redact(p.Name, p.Value);
                        changed |= !ReferenceEquals(redacted, p.Value);
                        return new LogEventProperty(p.Name, redacted);
                    })
                    .ToList();
                return changed ? new StructureValue(properties, structure.TypeTag) : value;
            }

            case SequenceValue sequence:
            {
                var changed = false;
                var elements = sequence
                    .Elements.Select(e =>
                    {
                        var redacted = Redact(name, e);
                        changed |= !ReferenceEquals(redacted, e);
                        return redacted;
                    })
                    .ToList();
                return changed ? new SequenceValue(elements) : value;
            }

            case DictionaryValue dictionary:
            {
                var changed = false;
                var elements = dictionary
                    .Elements.Select(e =>
                    {
                        var key = e.Key.Value?.ToString() ?? string.Empty;
                        var redacted = Redact(key, e.Value);
                        changed |= !ReferenceEquals(redacted, e.Value);
                        return new KeyValuePair<ScalarValue, LogEventPropertyValue>(
                            e.Key,
                            redacted
                        );
                    })
                    .ToList();
                return changed ? new DictionaryValue(elements) : value;
            }

            default:
                return value;
        }
    }

    /// <summary>Masks emails, JWTs and bearer tokens; returns the same instance if there were none.</summary>
    public static string MaskPatterns(string text)
    {
        if (text.Length < 6)
        {
            return text;
        }

        var masked = BearerToken().Replace(text, "Bearer " + PlaceholderRedactor.Placeholder);
        masked = Jwt().Replace(masked, PlaceholderRedactor.Placeholder);
        masked = Email().Replace(masked, PlaceholderRedactor.Placeholder);
        return masked == text ? text : masked;
    }

    [GeneratedRegex(@"[^\s@""'<>(),;:]+@[^\s@""'<>(),;:]+\.[A-Za-z]{2,}")]
    private static partial Regex Email();

    [GeneratedRegex(@"eyJ[A-Za-z0-9_-]{5,}\.[A-Za-z0-9_-]{5,}\.[A-Za-z0-9_-]*")]
    private static partial Regex Jwt();

    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9._~+/=-]+", RegexOptions.IgnoreCase)]
    private static partial Regex BearerToken();
}
