namespace AppTemplate.UnitTests.SharedKernel;

public class ValueObjectCompareTo
{
    /// <summary>A component that isn't IComparable.</summary>
    private sealed record Tag(string Name)
    {
        public override string ToString() => Name;
    }

    private sealed class Tagged(string tag) : ValueObject
    {
        private readonly Tag _tag = new(tag);

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return _tag;
        }
    }

    [Fact]
    public void OrdersNonComparableComponentsOneWayOnly()
    {
        var a = new Tagged("a");
        var b = new Tagged("b");

        a.CompareTo(b).ShouldBeLessThan(0);
        b.CompareTo(a).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void IsZeroForEqualComponents()
    {
        new Tagged("a").CompareTo(new Tagged("a")).ShouldBe(0);
    }
}
