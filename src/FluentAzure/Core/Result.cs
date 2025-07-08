using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace FluentAzure.Core;

/// <summary>
/// Represents the result of an operation that can either succeed with a value or fail with errors.
/// This is an immutable, thread-safe implementation of the Result monad pattern.
/// </summary>
/// <typeparam name="T">The type of the success value</typeparam>
public readonly struct Result<T>
{
    private readonly T? _value;
    private readonly ImmutableList<string> _errors;
    private readonly bool _isSuccess;

    /// <summary>
    /// Initializes a new successful result with the specified value.
    /// </summary>
    /// <param name="value">The success value</param>
    private Result(T value)
    {
        _value = value;
        _errors = ImmutableList<string>.Empty;
        _isSuccess = true;
    }

    /// <summary>
    /// Initializes a new failed result with the specified errors.
    /// </summary>
    /// <param name="errors">The collection of error messages</param>
    private Result(IEnumerable<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        var errorList = errors.ToImmutableList();
        if (errorList.Count == 0)
            throw new ArgumentException("At least one error must be provided", nameof(errors));

        _value = default;
        _errors = errorList;
        _isSuccess = false;
    }

    /// <summary>
    /// Gets a value indicating whether the result represents a success.
    /// </summary>
    [MemberNotNullWhen(true, nameof(_value))]
    public bool IsSuccess => _isSuccess;

    /// <summary>
    /// Gets a value indicating whether the result represents a failure.
    /// </summary>
    [MemberNotNullWhen(false, nameof(_value))]
    public bool IsFailure => !_isSuccess;

    /// <summary>
    /// Gets the success value. Only valid when <see cref="IsSuccess"/> is true.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when called on a failed result</exception>
    public T Value =>
        _isSuccess
            ? _value!
            : throw new InvalidOperationException("Cannot access value of a failed result");

    /// <summary>
    /// Gets the collection of error messages. Only valid when <see cref="IsFailure"/> is true.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when called on a successful result</exception>
    public IReadOnlyList<string> Errors =>
        _isSuccess
            ? throw new InvalidOperationException("Cannot access errors of a successful result")
            : _errors;

    /// <summary>
    /// Creates a successful result with the specified value.
    /// </summary>
    /// <param name="value">The success value</param>
    /// <returns>A successful result containing the specified value</returns>
    public static Result<T> Success(T value) => new(value);

    /// <summary>
    /// Creates a failed result with the specified error message.
    /// </summary>
    /// <param name="error">The error message</param>
    /// <returns>A failed result containing the specified error</returns>
    public static Result<T> Error(string error) => new(new[] { error });

    /// <summary>
    /// Creates a failed result with the specified error messages.
    /// </summary>
    /// <param name="errors">The collection of error messages</param>
    /// <returns>A failed result containing the specified errors</returns>
    public static Result<T> Error(IEnumerable<string> errors) => new(errors);

    /// <summary>
    /// Creates a failed result with the specified error messages.
    /// </summary>
    /// <param name="errors">The error messages</param>
    /// <returns>A failed result containing the specified errors</returns>
    public static Result<T> Error(params string[] errors) => new(errors);

    /// <summary>
    /// Transforms the success value using the specified function.
    /// If the result is a failure, the errors are preserved.
    /// </summary>
    /// <typeparam name="TResult">The type of the transformed value</typeparam>
    /// <param name="transform">The transformation function</param>
    /// <returns>A new result with the transformed value or the original errors</returns>
    public Result<TResult> Map<TResult>(Func<T, TResult> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);

        return _isSuccess
            ? Result<TResult>.Success(transform(_value!))
            : Result<TResult>.Error(_errors);
    }

    /// <summary>
    /// Transforms the success value using the specified function that returns a Result.
    /// If the result is a failure, the errors are preserved.
    /// This is the monadic bind operation.
    /// </summary>
    /// <typeparam name="TResult">The type of the transformed value</typeparam>
    /// <param name="transform">The transformation function that returns a Result</param>
    /// <returns>A new result with the transformed value or accumulated errors</returns>
    public Result<TResult> Bind<TResult>(Func<T, Result<TResult>> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);

        return _isSuccess ? transform(_value!) : Result<TResult>.Error(_errors);
    }

    /// <summary>
    /// Executes one of two functions based on whether the result is a success or failure.
    /// </summary>
    /// <typeparam name="TResult">The type of the result</typeparam>
    /// <param name="onSuccess">The function to execute if the result is a success</param>
    /// <param name="onFailure">The function to execute if the result is a failure</param>
    /// <returns>The result of the executed function</returns>
    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<IReadOnlyList<string>, TResult> onFailure
    )
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return _isSuccess ? onSuccess(_value!) : onFailure(_errors);
    }

    /// <summary>
    /// Executes one of two actions based on whether the result is a success or failure.
    /// </summary>
    /// <param name="onSuccess">The action to execute if the result is a success</param>
    /// <param name="onFailure">The action to execute if the result is a failure</param>
    public void Match(Action<T> onSuccess, Action<IReadOnlyList<string>> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        if (_isSuccess)
            onSuccess(_value!);
        else
            onFailure(_errors);
    }

    /// <summary>
    /// Combines this result with another result. If both are successful, combines their values using the specified function.
    /// If either is a failure, combines all errors.
    /// </summary>
    /// <typeparam name="TOther">The type of the other result's value</typeparam>
    /// <typeparam name="TResult">The type of the combined result</typeparam>
    /// <param name="other">The other result to combine with</param>
    /// <param name="combiner">The function to combine successful values</param>
    /// <returns>A result with the combined value or all accumulated errors</returns>
    public Result<TResult> Combine<TOther, TResult>(
        Result<TOther> other,
        Func<T, TOther, TResult> combiner
    )
    {
        ArgumentNullException.ThrowIfNull(combiner);

        return (_isSuccess, other._isSuccess) switch
        {
            (true, true) => Result<TResult>.Success(combiner(_value!, other._value!)),
            (true, false) => Result<TResult>.Error(other._errors),
            (false, true) => Result<TResult>.Error(_errors),
            (false, false) => Result<TResult>.Error(_errors.Concat(other._errors)),
        };
    }

    /// <summary>
    /// Implicitly converts a value to a successful result.
    /// </summary>
    /// <param name="value">The value to convert</param>
    public static implicit operator Result<T>(T value) => Success(value);

    /// <summary>
    /// Implicitly converts an error message to a failed result.
    /// </summary>
    /// <param name="error">The error message to convert</param>
    public static implicit operator Result<T>(string error) => Error(error);

    /// <summary>
    /// Implicitly converts an array of error messages to a failed result.
    /// </summary>
    /// <param name="errors">The error messages to convert</param>
    public static implicit operator Result<T>(string[] errors) => Error(errors);

    /// <summary>
    /// Returns a string representation of the result.
    /// </summary>
    /// <returns>A string representation of the result</returns>
    public override string ToString()
    {
        return _isSuccess ? $"Success({_value})" : $"Error({string.Join(", ", _errors)})";
    }

    /// <summary>
    /// Determines whether the specified object is equal to this result.
    /// </summary>
    /// <param name="obj">The object to compare with this result</param>
    /// <returns>true if the specified object is equal to this result; otherwise, false</returns>
    public override bool Equals(object? obj)
    {
        return obj is Result<T> other && Equals(other);
    }

    /// <summary>
    /// Determines whether the specified result is equal to this result.
    /// </summary>
    /// <param name="other">The result to compare with this result</param>
    /// <returns>true if the specified result is equal to this result; otherwise, false</returns>
    public bool Equals(Result<T> other)
    {
        if (_isSuccess != other._isSuccess)
            return false;

        if (_isSuccess)
            return EqualityComparer<T>.Default.Equals(_value, other._value);

        return _errors.SequenceEqual(other._errors);
    }

    /// <summary>
    /// Returns the hash code for this result.
    /// </summary>
    /// <returns>A hash code for this result</returns>
    public override int GetHashCode()
    {
        return _isSuccess
            ? HashCode.Combine(_isSuccess, _value)
            : HashCode.Combine(_isSuccess, _errors.Count > 0 ? _errors[0] : string.Empty);
    }

    /// <summary>
    /// Determines whether two results are equal.
    /// </summary>
    /// <param name="left">The first result to compare</param>
    /// <param name="right">The second result to compare</param>
    /// <returns>true if the results are equal; otherwise, false</returns>
    public static bool operator ==(Result<T> left, Result<T> right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two results are not equal.
    /// </summary>
    /// <param name="left">The first result to compare</param>
    /// <param name="right">The second result to compare</param>
    /// <returns>true if the results are not equal; otherwise, false</returns>
    public static bool operator !=(Result<T> left, Result<T> right)
    {
        return !left.Equals(right);
    }
}

/// <summary>
/// Provides utility methods for working with Results.
/// </summary>
public static class Result
{
    /// <summary>
    /// Creates a successful result with the specified value.
    /// </summary>
    /// <typeparam name="T">The type of the success value</typeparam>
    /// <param name="value">The success value</param>
    /// <returns>A successful result containing the specified value</returns>
    public static Result<T> Success<T>(T value) => Result<T>.Success(value);

    /// <summary>
    /// Creates a failed result with the specified error message.
    /// </summary>
    /// <typeparam name="T">The type of the success value</typeparam>
    /// <param name="error">The error message</param>
    /// <returns>A failed result containing the specified error</returns>
    public static Result<T> Error<T>(string error) => Result<T>.Error(error);

    /// <summary>
    /// Creates a failed result with the specified error messages.
    /// </summary>
    /// <typeparam name="T">The type of the success value</typeparam>
    /// <param name="errors">The collection of error messages</param>
    /// <returns>A failed result containing the specified errors</returns>
    public static Result<T> Error<T>(IEnumerable<string> errors) => Result<T>.Error(errors);

    /// <summary>
    /// Creates a failed result with the specified error messages.
    /// </summary>
    /// <typeparam name="T">The type of the success value</typeparam>
    /// <param name="errors">The error messages</param>
    /// <returns>A failed result containing the specified errors</returns>
    public static Result<T> Error<T>(params string[] errors) => Result<T>.Error(errors);

    /// <summary>
    /// Combines multiple results into a single result. If all are successful, returns a successful result with all values.
    /// If any are failures, returns a failed result with all accumulated errors.
    /// </summary>
    /// <typeparam name="T">The type of the result values</typeparam>
    /// <param name="results">The results to combine</param>
    /// <returns>A result containing all values or all accumulated errors</returns>
    public static Result<IReadOnlyList<T>> Combine<T>(IEnumerable<Result<T>> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var resultsList = results.ToList();
        var errors = new List<string>();
        var values = new List<T>();

        foreach (var result in resultsList)
        {
            if (result.IsSuccess)
                values.Add(result.Value);
            else
                errors.AddRange(result.Errors);
        }

        return errors.Count == 0
            ? Success<IReadOnlyList<T>>(values)
            : Error<IReadOnlyList<T>>(errors);
    }

    /// <summary>
    /// Combines multiple results into a single result. If all are successful, returns a successful result with all values.
    /// If any are failures, returns a failed result with all accumulated errors.
    /// </summary>
    /// <typeparam name="T">The type of the result values</typeparam>
    /// <param name="results">The results to combine</param>
    /// <returns>A result containing all values or all accumulated errors</returns>
    public static Result<IReadOnlyList<T>> Combine<T>(params Result<T>[] results)
    {
        return Combine(results.AsEnumerable());
    }
}
