namespace AppTemplate.Core.Aggregates.UserAggregate;

[ValueObject<string>(conversions: Conversions.SystemTextJson)]
public readonly partial struct UserName
{
    public const int MaxLength = 100;

    private static Validation Validate(in string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return Validation.Invalid("Name cannot be empty");
        }

        if (name.Length > MaxLength)
        {
            return Validation.Invalid($"Name cannot be longer than {MaxLength} characters");
        }

        return Validation.Ok;
    }
}
