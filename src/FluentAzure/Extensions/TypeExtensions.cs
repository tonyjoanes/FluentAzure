using FluentAzure.Core;

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

    /// <summary>
    /// Attempts to convert a string value to the specified type.
    /// </summary>
    /// <typeparam name="T">The target type to convert to</typeparam>
    /// <param name="value">The string value to convert</param>
    /// <returns>Success with the converted value if conversion succeeds, Error otherwise</returns>
    public static Result<T> TryConvert<T>(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return Result<T>.Error("Value cannot be null or empty");
        }

        try
        {
            var targetType = typeof(T);

            // Handle common primitive types
            if (targetType == typeof(string))
            {
                return Result<T>.Success((T)(object)value);
            }

            if (targetType == typeof(int))
            {
                if (int.TryParse(value, out var result))
                {
                    return Result<T>.Success((T)(object)result);
                }
                return Result<T>.Error($"Cannot convert '{value}' to int");
            }

            if (targetType == typeof(long))
            {
                if (long.TryParse(value, out var result))
                {
                    return Result<T>.Success((T)(object)result);
                }
                return Result<T>.Error($"Cannot convert '{value}' to long");
            }

            if (targetType == typeof(double))
            {
                if (double.TryParse(value, out var result))
                {
                    return Result<T>.Success((T)(object)result);
                }
                return Result<T>.Error($"Cannot convert '{value}' to double");
            }

            if (targetType == typeof(decimal))
            {
                if (decimal.TryParse(value, out var result))
                {
                    return Result<T>.Success((T)(object)result);
                }
                return Result<T>.Error($"Cannot convert '{value}' to decimal");
            }

            if (targetType == typeof(bool))
            {
                if (bool.TryParse(value, out var result))
                {
                    return Result<T>.Success((T)(object)result);
                }
                return Result<T>.Error($"Cannot convert '{value}' to bool");
            }

            if (targetType == typeof(DateTime))
            {
                if (DateTime.TryParse(value, out var result))
                {
                    return Result<T>.Success((T)(object)result);
                }
                return Result<T>.Error($"Cannot convert '{value}' to DateTime");
            }

            if (targetType == typeof(TimeSpan))
            {
                if (TimeSpan.TryParse(value, out var result))
                {
                    return Result<T>.Success((T)(object)result);
                }
                return Result<T>.Error($"Cannot convert '{value}' to TimeSpan");
            }

            if (targetType == typeof(Guid))
            {
                if (Guid.TryParse(value, out var result))
                {
                    return Result<T>.Success((T)(object)result);
                }
                return Result<T>.Error($"Cannot convert '{value}' to Guid");
            }

            if (targetType == typeof(Uri))
            {
                if (Uri.TryCreate(value, UriKind.RelativeOrAbsolute, out var result))
                {
                    return Result<T>.Success((T)(object)result);
                }
                return Result<T>.Error($"Cannot convert '{value}' to Uri");
            }

            // Handle nullable types
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                var underlyingType = Nullable.GetUnderlyingType(targetType);
                if (underlyingType != null)
                {
                    // Recursively try to convert to the underlying type
                    var method = typeof(TypeExtensions).GetMethod("TryConvert", new[] { typeof(string) });
                    var genericMethod = method?.MakeGenericMethod(underlyingType);
                    var result = genericMethod?.Invoke(null, new object[] { value });

                    if (result is Result<object> typedResult && typedResult.IsSuccess)
                    {
                        // Create nullable instance
                        var nullableInstance = Activator.CreateInstance(targetType, typedResult.Value);
                        return Result<T>.Success((T)nullableInstance!);
                    }
                }
            }

            // Handle enums
            if (targetType.IsEnum)
            {
                if (Enum.TryParse(targetType, value, true, out var result))
                {
                    return Result<T>.Success((T)result);
                }
                return Result<T>.Error($"Cannot convert '{value}' to enum {targetType.Name}");
            }

            // Fallback to Convert.ChangeType for other types
            try
            {
                var converted = Convert.ChangeType(value, targetType);
                return Result<T>.Success((T)converted);
            }
            catch (Exception ex)
            {
                return Result<T>.Error($"Cannot convert '{value}' to {targetType.Name}: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            return Result<T>.Error($"Conversion failed: {ex.Message}");
        }
    }
}
