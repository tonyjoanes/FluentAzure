using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using FluentAzure.Binding;
using FluentAzure.Core;

namespace FluentAzure.Extensions;

/// <summary>
/// Extension methods for configuration binding with enhanced features.
/// This class is used to bind configuration to a strongly-typed object.
/// It is used in the FluentAzure.Extensions namespace.
/// </summary>
public static class BindingExtensions
{
    /// <summary>
    /// Binds configuration to a strongly-typed object with validation.
    /// </summary>
    /// <typeparam name="T">The type of object to bind to.</typeparam>
    /// <param name="result">The configuration result.</param>
    /// <param name="options">Optional binding options.</param>
    /// <returns>A result containing the bound instance or validation errors.</returns>
    public static Result<T> Bind<T>(
        this Result<Dictionary<string, string>> result,
        BindingOptions? options = null
    )
        where T : class
    {
        return result.Bind(config => EnhancedConfigurationBinder.Bind<T>(config, options));
    }

    /// <summary>
    /// Binds configuration to a strongly-typed object using JSON deserialization.
    /// </summary>
    /// <typeparam name="T">The type of object to bind to.</typeparam>
    /// <param name="result">The configuration result.</param>
    /// <param name="options">Optional binding options.</param>
    /// <returns>A result containing the bound instance or errors.</returns>
    public static Result<T> BindJson<T>(
        this Result<Dictionary<string, string>> result,
        BindingOptions? options = null
    )
        where T : class
    {
        return result.Bind(config => EnhancedConfigurationBinder.BindJson<T>(config, options));
    }

    /// <summary>
    /// Binds configuration to a record type with validation.
    /// </summary>
    /// <typeparam name="T">The record type to bind to.</typeparam>
    /// <param name="result">The configuration result.</param>
    /// <param name="options">Optional binding options.</param>
    /// <returns>A result containing the bound record or validation errors.</returns>
    public static Result<T> BindRecord<T>(
        this Result<Dictionary<string, string>> result,
        BindingOptions? options = null
    )
        where T : class
    {
        var bindingOptions = options ?? new BindingOptions();
        bindingOptions.EnableValidation = true;

        return result.BindJson<T>(bindingOptions);
    }

    /// <summary>
    /// Binds configuration to an object with custom JSON options.
    /// </summary>
    /// <typeparam name="T">The type of object to bind to.</typeparam>
    /// <param name="result">The configuration result.</param>
    /// <param name="jsonOptions">Custom JSON serialization options.</param>
    /// <returns>A result containing the bound instance or errors.</returns>
    public static Result<T> BindWithJsonOptions<T>(
        this Result<Dictionary<string, string>> result,
        JsonSerializerOptions jsonOptions
    )
        where T : class
    {
        var options = new BindingOptions { JsonOptions = jsonOptions };
        return result.BindJson<T>(options);
    }

    /// <summary>
    /// Binds configuration to an object with case-sensitive key matching.
    /// </summary>
    /// <typeparam name="T">The type of object to bind to.</typeparam>
    /// <param name="result">The configuration result.</param>
    /// <returns>A result containing the bound instance or errors.</returns>
    public static Result<T> BindCaseSensitive<T>(this Result<Dictionary<string, string>> result)
        where T : class
    {
        var options = new BindingOptions { CaseSensitive = true };
        return result.Bind<T>(options);
    }

    /// <summary>
    /// Binds configuration to an object without validation.
    /// </summary>
    /// <typeparam name="T">The type of object to bind to.</typeparam>
    /// <param name="result">The configuration result.</param>
    /// <returns>A result containing the bound instance or errors.</returns>
    public static Result<T> BindWithoutValidation<T>(this Result<Dictionary<string, string>> result)
        where T : class
    {
        var options = new BindingOptions { EnableValidation = false };
        return result.Bind<T>(options);
    }

    /// <summary>
    /// Binds configuration to a collection type.
    /// </summary>
    /// <typeparam name="T">The collection type to bind to.</typeparam>
    /// <param name="result">The configuration result.</param>
    /// <param name="options">Optional binding options.</param>
    /// <returns>A result containing the bound collection or errors.</returns>
    public static Result<T> BindCollection<T>(
        this Result<Dictionary<string, string>> result,
        BindingOptions? options = null
    )
        where T : class
    {
        return result.Bind<T>(options);
    }

    /// <summary>
    /// Binds configuration to a list of objects.
    /// </summary>
    /// <typeparam name="T">The type of objects in the list.</typeparam>
    /// <param name="result">The configuration result.</param>
    /// <param name="listKey">The configuration key containing the list.</param>
    /// <param name="options">Optional binding options.</param>
    /// <returns>A result containing the bound list or errors.</returns>
    public static Result<List<T>> BindList<T>(
        this Result<Dictionary<string, string>> result,
        string listKey,
        BindingOptions? options = null
    )
        where T : class
    {
        return result.Bind(config =>
        {
            // Filter configuration to only include keys for this list
            var listConfig = config
                .Where(kvp =>
                    kvp.Key.StartsWith(listKey + "__", StringComparison.OrdinalIgnoreCase)
                )
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            if (!listConfig.Any())
            {
                return Result<List<T>>.Error($"No configuration found for list key '{listKey}'");
            }

            // Convert to JSON array format
            var jsonArray = ConvertListToJsonArray(listConfig, listKey);

            try
            {
                var bindingOptions = options ?? new BindingOptions();
                var list = JsonSerializer.Deserialize<List<T>>(
                    jsonArray,
                    bindingOptions.JsonOptions ?? new JsonSerializerOptions()
                );

                if (list == null)
                {
                    return Result<List<T>>.Error("Failed to deserialize list");
                }

                // Validate if enabled
                if (bindingOptions.EnableValidation)
                {
                    var errors = new List<string>();
                    foreach (var item in list)
                    {
                        var validationContext = new ValidationContext(item);
                        var validationResults = new List<ValidationResult>();

                        if (
                            !Validator.TryValidateObject(
                                item,
                                validationContext,
                                validationResults,
                                true
                            )
                        )
                        {
                            errors.AddRange(
                                validationResults.Select(r => r.ErrorMessage ?? "Validation failed")
                            );
                        }
                    }

                    if (errors.Any())
                    {
                        return Result<List<T>>.Error(errors);
                    }
                }

                return Result<List<T>>.Success(list);
            }
            catch (JsonException ex)
            {
                return Result<List<T>>.Error($"JSON deserialization failed: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Binds configuration to a dictionary.
    /// </summary>
    /// <typeparam name="TKey">The type of dictionary keys.</typeparam>
    /// <typeparam name="TValue">The type of dictionary values.</typeparam>
    /// <param name="result">The configuration result.</param>
    /// <param name="dictionaryKey">The configuration key containing the dictionary.</param>
    /// <param name="options">Optional binding options.</param>
    /// <returns>A result containing the bound dictionary or errors.</returns>
    public static Result<Dictionary<TKey, TValue>> BindDictionary<TKey, TValue>(
        this Result<Dictionary<string, string>> result,
        string dictionaryKey,
        BindingOptions? options = null
    )
        where TKey : notnull
        where TValue : class
    {
        return result.Bind(config =>
        {
            // Filter configuration to only include keys for this dictionary
            var dictConfig = config
                .Where(kvp =>
                    kvp.Key.StartsWith(dictionaryKey + "__", StringComparison.OrdinalIgnoreCase)
                )
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            if (!dictConfig.Any())
            {
                return Result<Dictionary<TKey, TValue>>.Error(
                    $"No configuration found for dictionary key '{dictionaryKey}'"
                );
            }

            // Convert to JSON object format
            var jsonObject = ConvertDictionaryToJsonObject(dictConfig, dictionaryKey);

            try
            {
                var bindingOptions = options ?? new BindingOptions();
                var dictionary = JsonSerializer.Deserialize<Dictionary<TKey, TValue>>(
                    jsonObject,
                    bindingOptions.JsonOptions ?? new JsonSerializerOptions()
                );

                if (dictionary == null)
                {
                    return Result<Dictionary<TKey, TValue>>.Error(
                        "Failed to deserialize dictionary"
                    );
                }

                return Result<Dictionary<TKey, TValue>>.Success(dictionary);
            }
            catch (JsonException ex)
            {
                return Result<Dictionary<TKey, TValue>>.Error(
                    $"JSON deserialization failed: {ex.Message}"
                );
            }
        });
    }

    /// <summary>
    /// Binds configuration to an object with custom validation.
    /// </summary>
    /// <typeparam name="T">The type of object to bind to.</typeparam>
    /// <param name="result">The configuration result.</param>
    /// <param name="validator">Custom validation function.</param>
    /// <returns>A result containing the bound instance or validation errors.</returns>
    public static Result<T> BindWithValidation<T>(
        this Result<Dictionary<string, string>> result,
        Func<T, Result<string>> validator
    )
        where T : class
    {
        return result
            .Bind<T>()
            .Bind(instance =>
            {
                var validationResult = validator(instance);
                return validationResult.IsSuccess
                    ? Result<T>.Success(instance)
                    : Result<T>.Error(validationResult.Errors);
            });
    }

    /// <summary>
    /// Binds configuration to an object with transformation.
    /// </summary>
    /// <typeparam name="T">The type of object to bind to.</typeparam>
    /// <typeparam name="TResult">The type of the transformed result.</typeparam>
    /// <param name="result">The configuration result.</param>
    /// <param name="transformer">Transformation function.</param>
    /// <returns>A result containing the transformed instance or errors.</returns>
    public static Result<TResult> BindAndTransform<T, TResult>(
        this Result<Dictionary<string, string>> result,
        Func<T, TResult> transformer
    )
        where T : class
        where TResult : class
    {
        return result.Bind<T>().Map(transformer);
    }

    private static string ConvertListToJsonArray(Dictionary<string, string> config, string listKey)
    {
        var items = new List<Dictionary<string, object>>();
        var groups = config
            .GroupBy(kvp => kvp.Key.Split(new[] { "__" }, StringSplitOptions.RemoveEmptyEntries)[1]) // Get index
            .OrderBy(g => int.Parse(g.Key));

        foreach (var group in groups)
        {
            var item = new Dictionary<string, object>();
            foreach (var kvp in group)
            {
                var keys = kvp.Key.Split(new[] { "__" }, StringSplitOptions.RemoveEmptyEntries);
                if (keys.Length > 2)
                {
                    var propertyPath = string.Join(".", keys.Skip(2));
                    SetNestedValue(item, propertyPath, ParseValue(kvp.Value));
                }
            }
            items.Add(item);
        }

        return JsonSerializer.Serialize(items);
    }

    private static string ConvertDictionaryToJsonObject(
        Dictionary<string, string> config,
        string dictionaryKey
    )
    {
        var dictionary = new Dictionary<string, object>();

        foreach (var kvp in config)
        {
            var keys = kvp.Key.Split(new[] { "__" }, StringSplitOptions.RemoveEmptyEntries);
            if (keys.Length > 1)
            {
                var dictKey = keys[1];
                if (keys.Length > 2)
                {
                    var propertyPath = string.Join(".", keys.Skip(2));
                    if (!dictionary.ContainsKey(dictKey))
                    {
                        dictionary[dictKey] = new Dictionary<string, object>();
                    }
                    SetNestedValue(
                        (Dictionary<string, object>)dictionary[dictKey],
                        propertyPath,
                        ParseValue(kvp.Value)
                    );
                }
                else
                {
                    dictionary[dictKey] = ParseValue(kvp.Value);
                }
            }
        }

        return JsonSerializer.Serialize(dictionary);
    }

    private static void SetNestedValue(Dictionary<string, object> dict, string path, object value)
    {
        var keys = path.Split('.');
        var current = dict;

        for (int i = 0; i < keys.Length - 1; i++)
        {
            var key = keys[i];
            if (!current.ContainsKey(key))
            {
                current[key] = new Dictionary<string, object>();
            }
            current = (Dictionary<string, object>)current[key];
        }

        current[keys[keys.Length - 1]] = value;
    }

    private static object ParseValue(string value)
    {
        // Try to parse as different types
        if (bool.TryParse(value, out var boolValue))
            return boolValue;
        if (int.TryParse(value, out var intValue))
            return intValue;
        if (double.TryParse(value, out var doubleValue))
            return doubleValue;
        if (DateTime.TryParse(value, out var dateValue))
            return dateValue;

        return value;
    }
}
