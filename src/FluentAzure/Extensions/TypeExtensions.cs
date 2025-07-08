/// <summary>
/// Provides extension methods for type-related operations.
/// </summary>
public static class TypeExtensions
{
    /// <summary>
    /// Determines whether the specified type is a collection type.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type is a collection type; otherwise, false.</returns>
    public static bool IsCollectionType(this Type type)
    {
        return type.IsArray
            || (
                type.IsGenericType
                && typeof(IEnumerable<>).IsAssignableFrom(type.GetGenericTypeDefinition())
            );
    }
}
