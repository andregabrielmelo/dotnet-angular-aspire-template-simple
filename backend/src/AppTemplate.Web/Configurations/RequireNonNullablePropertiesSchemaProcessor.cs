using NJsonSchema;
using NJsonSchema.Generation;

namespace AppTemplate.Web.Configurations;

/// <summary>
/// Marks every non-nullable property of a schema as <c>required</c>. NSwag leaves
/// <c>required</c> empty, so generated clients (the Angular types) would make every field
/// optional - <c>id?: number</c> - even though System.Text.Json always writes it. Nullable
/// properties stay optional, which also matches request DTOs, where null means "not sent".
/// </summary>
public sealed class RequireNonNullablePropertiesSchemaProcessor : ISchemaProcessor
{
    public void Process(SchemaProcessorContext context)
    {
        foreach (var property in context.Schema.ActualProperties.Values)
        {
            if (!property.IsNullable(SchemaType.OpenApi3))
            {
                property.IsRequired = true;
            }
        }
    }
}
