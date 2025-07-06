using System;
using System.Collections.Generic;
using System.Reflection;
using System.ComponentModel;
using System.Linq;
using FluentAzure.Core;

namespace FluentAzure.Binding;

/// <summary>
/// Provides functionality to bind configuration values to strongly-typed objects.
/// </summary>
public static class ConfigurationBinder
{
    /// <summary>
    /// Binds configuration values to a strongly-typed object.
    /// </summary>
    /// <typeparam name="T">The type of object to bind to.</typeparam>
    /// <param name="configuration">The configuration key-value pairs.</param>
    /// <param name="instance">The instance to bind values to.</param>
    /// <returns>A result indicating success or failure of the binding operation.</returns>
    public static Result<T> Bind<T>(Dictionary<string, string> configuration, T instance)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(instance);

        var errors = new List<string>();

        try
        {
            BindInternal(configuration, instance, "", errors);

            return errors.Count > 0
                ? Result<T>.Error(errors)
                : Result<T>.Success(instance);
        }
        catch (Exception ex)
        {
            return Result<T>.Error($"Failed to bind configuration to type {typeof(T).Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a new instance of the specified type and binds configuration values to it.
    /// </summary>
    /// <typeparam name="T">The type of object to create and bind to.</typeparam>
    /// <param name="configuration">The configuration key-value pairs.</param>
    /// <returns>A result containing the bound instance or errors.</returns>
    public static Result<T> Bind<T>(Dictionary<string, string> configuration)
        where T : class, new()
    {
        var instance = new T();
        return Bind(configuration, instance);
    }

    private static void BindInternal(Dictionary<string, string> configuration, object instance, string prefix, List<string> errors)
    {
        var type = instance.GetType();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && p.SetMethod != null)
            .ToList();

        foreach (var property in properties)
        {
            var configKey = string.IsNullOrEmpty(prefix)
                ? property.Name
                : $"{prefix}__{property.Name}";

            // Check if this is a simple value property
            if (IsSimpleType(property.PropertyType))
            {
                var configValue = FindConfigurationValue(configuration, configKey);
                if (configValue != null)
                {
                    try
                    {
                        var convertedValue = ConvertValue(configValue, property.PropertyType);
                        property.SetValue(instance, convertedValue);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Failed to bind property '{configKey}' with value '{configValue}': {ex.Message}");
                    }
                }
            }
            // Check if this is a complex object that needs recursive binding
            else if (property.PropertyType.IsClass && property.PropertyType != typeof(string))
            {
                // Check if there are any configuration keys that start with this property's prefix
                var hasNestedKeys = configuration.Keys.Any(k => k.StartsWith(configKey + "__", StringComparison.OrdinalIgnoreCase));

                if (hasNestedKeys)
                {
                    // Get or create the nested object
                    var nestedInstance = property.GetValue(instance);
                    if (nestedInstance == null)
                    {
                        // Try to create a new instance if it has a parameterless constructor
                        if (property.PropertyType.GetConstructor(Type.EmptyTypes) != null)
                        {
                            nestedInstance = Activator.CreateInstance(property.PropertyType);
                            property.SetValue(instance, nestedInstance);
                        }
                        else
                        {
                            errors.Add($"Cannot create instance of type '{property.PropertyType.Name}' for property '{configKey}' - no parameterless constructor found");
                            continue;
                        }
                    }

                    // Recursively bind the nested object
                    BindInternal(configuration, nestedInstance!, configKey, errors);
                }
            }
        }
    }

    private static bool IsSimpleType(Type type)
    {
        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(type);
        if (underlyingType != null)
        {
            type = underlyingType;
        }

        return type.IsPrimitive ||
               type == typeof(string) ||
               type == typeof(DateTime) ||
               type == typeof(TimeSpan) ||
               type == typeof(Guid) ||
               type == typeof(Uri) ||
               type == typeof(decimal) ||
               type.IsEnum;
    }

    private static string? FindConfigurationValue(Dictionary<string, string> configuration, string key)
    {
        // Try exact match first
        if (configuration.TryGetValue(key, out var value))
        {
            return value;
        }

        // Try case-insensitive match
        var matchingKey = configuration.Keys.FirstOrDefault(k =>
            string.Equals(k, key, StringComparison.OrdinalIgnoreCase));

        return matchingKey != null ? configuration[matchingKey] : null;
    }

    private static object? ConvertValue(string value, Type targetType)
    {
        if (targetType == typeof(string))
        {
            return value;
        }

        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(targetType);
        if (underlyingType != null)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }
            targetType = underlyingType;
        }

        // Handle common types
        if (targetType == typeof(bool))
        {
            return bool.Parse(value);
        }
        if (targetType == typeof(int))
        {
            return int.Parse(value);
        }
        if (targetType == typeof(long))
        {
            return long.Parse(value);
        }
        if (targetType == typeof(double))
        {
            return double.Parse(value);
        }
        if (targetType == typeof(decimal))
        {
            return decimal.Parse(value);
        }
        if (targetType == typeof(DateTime))
        {
            return DateTime.Parse(value);
        }
        if (targetType == typeof(TimeSpan))
        {
            return TimeSpan.Parse(value);
        }
        if (targetType == typeof(Guid))
        {
            return Guid.Parse(value);
        }
        if (targetType == typeof(Uri))
        {
            return new Uri(value);
        }

        // Handle enums
        if (targetType.IsEnum)
        {
            return Enum.Parse(targetType, value, ignoreCase: true);
        }

        // Use TypeConverter as fallback
        var converter = TypeDescriptor.GetConverter(targetType);
        if (converter.CanConvertFrom(typeof(string)))
        {
            return converter.ConvertFromString(value);
        }

        throw new InvalidOperationException($"Cannot convert value '{value}' to type {targetType.Name}");
    }

    private static bool IsRequiredProperty(PropertyInfo property)
    {
        // In configuration binding, all properties are optional by default
        // They will use their default values if not specified in configuration
        // In a more sophisticated implementation, you might check for [Required] attributes
        // or other explicit requirement indicators
        return false;
    }
}
