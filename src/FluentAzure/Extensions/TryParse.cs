namespace FluentAzure.Extensions;

/// <summary>
/// Delegate for try-parse functions.
/// </summary>
/// <typeparam name="T">The type to parse to.</typeparam>
/// <param name="input">The input string.</param>
/// <param name="result">The parsed result.</param>
/// <returns>True if parsing succeeded, false otherwise.</returns>
public delegate bool TryParse<T>(string input, out T result);
