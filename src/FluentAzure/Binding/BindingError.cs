namespace FluentAzure.Binding;

/// <summary>
/// Represents a binding error with detailed information.
/// </summary>
public class BindingError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BindingError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="propertyPath">The path to the property that caused the error.</param>
    public BindingError(string message, string propertyPath)
    {
        Message = message;
        PropertyPath = propertyPath;
    }

    /// <summary>
    /// Gets the error message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the path to the property that caused the error.
    /// </summary>
    public string PropertyPath { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        return string.IsNullOrEmpty(PropertyPath) ? Message : $"{PropertyPath}: {Message}";
    }
}
