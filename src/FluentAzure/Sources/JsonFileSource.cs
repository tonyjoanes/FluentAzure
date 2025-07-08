using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAzure.Core;

namespace FluentAzure.Sources;

/// <summary>
/// Configuration source that loads values from JSON files.
/// </summary>
public class JsonFileSource : IConfigurationSource
{
    private readonly string _filePath;
    private readonly bool _optional;
    private Dictionary<string, string>? _values;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonFileSource"/> class.
    /// </summary>
    /// <param name="filePath">The path to the JSON configuration file.</param>
    /// <param name="priority">The priority of this configuration source.</param>
    /// <param name="optional">Whether the file is optional. If false, missing file will cause an error.</param>
    public JsonFileSource(string filePath, int priority = 50, bool optional = false)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        Priority = priority;
        _optional = optional;
    }

    /// <inheritdoc />
    public string Name => $"JsonFile({Path.GetFileName(_filePath)})";

    /// <inheritdoc />
    public int Priority { get; }

    /// <inheritdoc />
    public async Task<Result<Dictionary<string, string>>> LoadAsync()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                if (_optional)
                {
                    _values = new Dictionary<string, string>();
                    return Result<Dictionary<string, string>>.Success(_values);
                }
                else
                {
                    return Result<Dictionary<string, string>>.Error(
                        $"Required JSON configuration file '{_filePath}' was not found"
                    );
                }
            }

            var jsonContent = await File.ReadAllTextAsync(_filePath);
            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                _values = new Dictionary<string, string>();
                return Result<Dictionary<string, string>>.Success(_values);
            }

            var jsonDocument = JsonDocument.Parse(jsonContent);
            _values = FlattenJsonDocument(jsonDocument.RootElement);

            return Result<Dictionary<string, string>>.Success(_values);
        }
        catch (JsonException ex)
        {
            return Result<Dictionary<string, string>>.Error(
                $"Failed to parse JSON configuration file '{_filePath}': {ex.Message}"
            );
        }
        catch (Exception ex)
        {
            return Result<Dictionary<string, string>>.Error(
                $"Failed to load JSON configuration file '{_filePath}': {ex.Message}"
            );
        }
    }

    /// <inheritdoc />
    public bool ContainsKey(string key)
    {
        return _values?.ContainsKey(key) ?? false;
    }

    /// <inheritdoc />
    public string? GetValue(string key)
    {
        return _values?.TryGetValue(key, out var value) == true ? value : null;
    }

    private static Dictionary<string, string> FlattenJsonDocument(
        JsonElement element,
        string prefix = ""
    )
    {
        var result = new Dictionary<string, string>();

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var key = string.IsNullOrEmpty(prefix)
                        ? property.Name
                        : $"{prefix}__{property.Name}";
                    var childValues = FlattenJsonDocument(property.Value, key);
                    foreach (var childValue in childValues)
                    {
                        result[childValue.Key] = childValue.Value;
                    }
                }
                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    var key = $"{prefix}__{index}";
                    var childValues = FlattenJsonDocument(item, key);
                    foreach (var childValue in childValues)
                    {
                        result[childValue.Key] = childValue.Value;
                    }
                    index++;
                }
                break;

            case JsonValueKind.String:
                result[prefix] = element.GetString() ?? "";
                break;

            case JsonValueKind.Number:
                result[prefix] = element.GetRawText();
                break;

            case JsonValueKind.True:
                result[prefix] = "true";
                break;

            case JsonValueKind.False:
                result[prefix] = "false";
                break;

            case JsonValueKind.Null:
                result[prefix] = "";
                break;
        }

        return result;
    }
}
