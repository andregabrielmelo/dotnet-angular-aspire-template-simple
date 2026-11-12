namespace AppTemplate.Tests;

/// <summary>Trait for tests that need Docker (a real Postgres) - CI filters these out where Docker can't run.</summary>
public static class TestCategories
{
    public const string Name = "Category";
    public const string RequiresDocker = "RequiresDocker";
}
