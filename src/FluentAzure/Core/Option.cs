using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace FluentAzure.Core;

/// <summary>
/// Represents an optional value that can either contain a value (Some) or be empty (None).
/// This is an immutable, thread-safe implementation of the Option monad pattern.
/// </summary>
/// <typeparam name="T">The type of the optional value</typeparam>
public readonly struct Option<T>
{
    private readonly T? _value;
    private readonly bool _hasValue;

    /// <summary>
    /// Initializes a new Option with a value (Some).
    /// </summary>
    /// <param name="value">The value to wrap</param>
    private Option(T value)
    {
        _value = value;
        _hasValue = true;
    }

    /// <summary>
    /// Initializes a new empty Option (None).
    /// </summary>
    public Option()
    {
        _value = default;
        _hasValue = false;
    }

    /// <summary>
    /// Gets a value indicating whether this option contains a value.
    /// </summary>
    [MemberNotNullWhen(true, nameof(_value))]
    public bool HasValue => _hasValue;

    /// <summary>
    /// Gets a value indicating whether this option is empty.
    /// </summary>
    [MemberNotNullWhen(false, nameof(_value))]
    public bool IsNone => !_hasValue;

    /// <summary>
    /// Gets the value if present. Only valid when <see cref="HasValue"/> is true.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when called on an empty option</exception>
    public T Value =>
        _hasValue
            ? _value!
            : throw new InvalidOperationException("Cannot access value of an empty option");

    /// <summary>
    /// Creates an Option with a value (Some).
    /// </summary>
    /// <param name="value">The value to wrap</param>
    /// <returns>An Option containing the specified value</returns>
    public static Option<T> Some(T value) => new(value);

    /// <summary>
    /// Creates an empty Option (None).
    /// </summary>
    /// <returns>An empty Option</returns>
    public static Option<T> None() => new();

    /// <summary>
    /// Creates an Option from a potentially null value.
    /// </summary>
    /// <param name="value">The value to wrap</param>
    /// <returns>Some(value) if value is not null, None otherwise</returns>
    public static Option<T> FromNullable(T? value) => value is not null ? Some(value) : None();

    /// <summary>
    /// Transforms the value using the specified function if present.
    /// If the option is empty, returns None.
    /// </summary>
    /// <typeparam name="TResult">The type of the transformed value</typeparam>
    /// <param name="mapper">The transformation function</param>
    /// <returns>A new Option with the transformed value or None</returns>
    public Option<TResult> Map<TResult>(Func<T, TResult> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);

        return _hasValue ? Option<TResult>.Some(mapper(_value!)) : Option<TResult>.None();
    }

    /// <summary>
    /// Transforms the value using the specified function that returns an Option.
    /// If the option is empty, returns None.
    /// This is the monadic bind operation.
    /// </summary>
    /// <typeparam name="TResult">The type of the transformed value</typeparam>
    /// <param name="binder">The transformation function that returns an Option</param>
    /// <returns>A new Option with the transformed value or None</returns>
    public Option<TResult> Bind<TResult>(Func<T, Option<TResult>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);

        return _hasValue ? binder(_value!) : Option<TResult>.None();
    }

    /// <summary>
    /// Transforms the value using the specified function that returns an Option.
    /// Alias for Bind to match common FP terminology.
    /// </summary>
    /// <typeparam name="TResult">The type of the transformed value</typeparam>
    /// <param name="binder">The transformation function that returns an Option</param>
    /// <returns>A new Option with the transformed value or None</returns>
    public Option<TResult> FlatMap<TResult>(Func<T, Option<TResult>> binder) => Bind(binder);

    /// <summary>
    /// Executes one of two functions based on whether the option has a value.
    /// </summary>
    /// <typeparam name="TResult">The type of the result</typeparam>
    /// <param name="some">The function to execute if the option has a value</param>
    /// <param name="none">The function to execute if the option is empty</param>
    /// <returns>The result of the executed function</returns>
    public TResult Match<TResult>(Func<T, TResult> some, Func<TResult> none)
    {
        ArgumentNullException.ThrowIfNull(some);
        ArgumentNullException.ThrowIfNull(none);

        return _hasValue ? some(_value!) : none();
    }

    /// <summary>
    /// Executes one of two actions based on whether the option has a value.
    /// </summary>
    /// <param name="some">The action to execute if the option has a value</param>
    /// <param name="none">The action to execute if the option is empty</param>
    public void Match(Action<T> some, Action none)
    {
        ArgumentNullException.ThrowIfNull(some);
        ArgumentNullException.ThrowIfNull(none);

        if (_hasValue)
            some(_value!);
        else
            none();
    }

    /// <summary>
    /// Returns the value if present, otherwise returns the specified default value.
    /// </summary>
    /// <param name="defaultValue">The default value to return if the option is empty</param>
    /// <returns>The option value or the default value</returns>
    public T GetValueOrDefault(T defaultValue) => _hasValue ? _value! : defaultValue;

    /// <summary>
    /// Returns the value if present, otherwise returns the result of the specified function.
    /// </summary>
    /// <param name="defaultFactory">The function to execute if the option is empty</param>
    /// <returns>The option value or the result of the default factory</returns>
    public T GetValueOrDefault(Func<T> defaultFactory)
    {
        ArgumentNullException.ThrowIfNull(defaultFactory);

        return _hasValue ? _value! : defaultFactory();
    }

    /// <summary>
    /// Returns this option if it has a value, otherwise returns the alternative option.
    /// </summary>
    /// <param name="alternative">The alternative option</param>
    /// <returns>This option if it has a value, otherwise the alternative</returns>
    public Option<T> Or(Option<T> alternative) => _hasValue ? this : alternative;

    /// <summary>
    /// Returns this option if it has a value, otherwise returns the result of the alternative function.
    /// </summary>
    /// <param name="alternativeFactory">The function to execute if this option is empty</param>
    /// <returns>This option if it has a value, otherwise the result of the alternative factory</returns>
    public Option<T> Or(Func<Option<T>> alternativeFactory)
    {
        ArgumentNullException.ThrowIfNull(alternativeFactory);

        return _hasValue ? this : alternativeFactory();
    }

    /// <summary>
    /// Filters the option based on a predicate.
    /// </summary>
    /// <param name="predicate">The predicate to test the value</param>
    /// <returns>This option if it has a value and the predicate returns true, otherwise None</returns>
    public Option<T> Filter(Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        return _hasValue && predicate(_value!) ? this : None();
    }

    /// <summary>
    /// Filters the option based on a predicate.
    /// Alias for Filter to match common FP terminology.
    /// </summary>
    /// <param name="predicate">The predicate to test the value</param>
    /// <returns>This option if it has a value and the predicate returns true, otherwise None</returns>
    public Option<T> Where(Func<T, bool> predicate) => Filter(predicate);

    /// <summary>
    /// Executes the specified action if the option has a value.
    /// </summary>
    /// <param name="action">The action to execute</param>
    /// <returns>This option for chaining</returns>
    public Option<T> Do(Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_hasValue)
            action(_value!);

        return this;
    }

    /// <summary>
    /// Executes the specified action if the option has a value.
    /// Alias for Do to match common FP terminology.
    /// </summary>
    /// <param name="action">The action to execute</param>
    /// <returns>This option for chaining</returns>
    public Option<T> Tap(Action<T> action) => Do(action);

    /// <summary>
    /// Converts the option to a nullable value.
    /// </summary>
    /// <returns>The value if present, otherwise null</returns>
    public T? ToNullable() => _hasValue ? _value : default;

    /// <summary>
    /// Converts the option to a Result.
    /// </summary>
    /// <param name="errorMessage">The error message to use if the option is empty</param>
    /// <returns>Success with the value if present, otherwise Error with the specified message</returns>
    public Result<T> ToResult(string errorMessage) =>
        _hasValue ? Result<T>.Success(_value!) : Result<T>.Error(errorMessage);

    /// <summary>
    /// Converts the option to a Result.
    /// </summary>
    /// <param name="errorFactory">The function to generate an error message if the option is empty</param>
    /// <returns>Success with the value if present, otherwise Error with the generated message</returns>
    public Result<T> ToResult(Func<string> errorFactory)
    {
        ArgumentNullException.ThrowIfNull(errorFactory);

        return _hasValue ? Result<T>.Success(_value!) : Result<T>.Error(errorFactory());
    }

    /// <summary>
    /// Converts the option to an enumerable.
    /// </summary>
    /// <returns>An enumerable with the value if present, otherwise an empty enumerable</returns>
    public IEnumerable<T> ToEnumerable()
    {
        if (_hasValue)
            yield return _value!;
    }

    /// <summary>
    /// Implicitly converts a value to Some(value).
    /// </summary>
    /// <param name="value">The value to wrap</param>
    public static implicit operator Option<T>(T value) => Some(value);

    /// <summary>
    /// Explicitly converts an option to a nullable value.
    /// </summary>
    /// <param name="option">The option to convert</param>
    public static explicit operator T?(Option<T> option) => option.ToNullable();

    /// <summary>
    /// Returns a string representation of the option.
    /// </summary>
    /// <returns>A string representation of the option</returns>
    public override string ToString()
    {
        return _hasValue ? $"Some({_value})" : "None";
    }

    /// <summary>
    /// Determines whether the specified object is equal to this option.
    /// </summary>
    /// <param name="obj">The object to compare with this option</param>
    /// <returns>true if the specified object is equal to this option; otherwise, false</returns>
    public override bool Equals(object? obj)
    {
        return obj is Option<T> other && Equals(other);
    }

    /// <summary>
    /// Determines whether the specified option is equal to this option.
    /// </summary>
    /// <param name="other">The option to compare with this option</param>
    /// <returns>true if the specified option is equal to this option; otherwise, false</returns>
    public bool Equals(Option<T> other)
    {
        if (_hasValue != other._hasValue)
            return false;

        return _hasValue ? EqualityComparer<T>.Default.Equals(_value, other._value) : true; // Both are None
    }

    /// <summary>
    /// Returns the hash code for this option.
    /// </summary>
    /// <returns>A hash code for this option</returns>
    public override int GetHashCode()
    {
        return _hasValue ? HashCode.Combine(_hasValue, _value) : HashCode.Combine(_hasValue);
    }

    /// <summary>
    /// Determines whether two options are equal.
    /// </summary>
    /// <param name="left">The first option to compare</param>
    /// <param name="right">The second option to compare</param>
    /// <returns>true if the options are equal; otherwise, false</returns>
    public static bool operator ==(Option<T> left, Option<T> right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two options are not equal.
    /// </summary>
    /// <param name="left">The first option to compare</param>
    /// <param name="right">The second option to compare</param>
    /// <returns>true if the options are not equal; otherwise, false</returns>
    public static bool operator !=(Option<T> left, Option<T> right)
    {
        return !left.Equals(right);
    }
}

/// <summary>
/// Provides utility methods for working with Options.
/// </summary>
public static class Option
{
    /// <summary>
    /// Creates an Option with a value (Some).
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <param name="value">The value to wrap</param>
    /// <returns>An Option containing the specified value</returns>
    public static Option<T> Some<T>(T value) => Option<T>.Some(value);

    /// <summary>
    /// Creates an empty Option (None).
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <returns>An empty Option</returns>
    public static Option<T> None<T>() => Option<T>.None();

    /// <summary>
    /// Creates an Option from a potentially null value.
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <param name="value">The value to wrap</param>
    /// <returns>Some(value) if value is not null, None otherwise</returns>
    public static Option<T> FromNullable<T>(T? value) => Option<T>.FromNullable(value);

    /// <summary>
    /// Combines multiple options into a single option. If all have values, returns Some with all values.
    /// If any is None, returns None.
    /// </summary>
    /// <typeparam name="T">The type of the option values</typeparam>
    /// <param name="options">The options to combine</param>
    /// <returns>An option containing all values or None</returns>
    public static Option<IReadOnlyList<T>> Combine<T>(IEnumerable<Option<T>> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var optionsList = options.ToList();
        var values = new List<T>();

        foreach (var option in optionsList)
        {
            if (option.IsNone)
                return Option<IReadOnlyList<T>>.None();

            values.Add(option.Value);
        }

        return Option<IReadOnlyList<T>>.Some(values);
    }

    /// <summary>
    /// Combines multiple options into a single option. If all have values, returns Some with all values.
    /// If any is None, returns None.
    /// </summary>
    /// <typeparam name="T">The type of the option values</typeparam>
    /// <param name="options">The options to combine</param>
    /// <returns>An option containing all values or None</returns>
    public static Option<IReadOnlyList<T>> Combine<T>(params Option<T>[] options)
    {
        return Combine(options.AsEnumerable());
    }

    /// <summary>
    /// Traverses a collection, applying a function that returns an Option to each element.
    /// If all succeed, returns Some with all results. If any fails, returns None.
    /// </summary>
    /// <typeparam name="T">The type of the input elements</typeparam>
    /// <typeparam name="TResult">The type of the result elements</typeparam>
    /// <param name="items">The items to traverse</param>
    /// <param name="mapper">The function to apply to each item</param>
    /// <returns>An option containing all results or None</returns>
    public static Option<IReadOnlyList<TResult>> Traverse<T, TResult>(
        IEnumerable<T> items,
        Func<T, Option<TResult>> mapper
    )
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(mapper);

        var results = new List<TResult>();

        foreach (var item in items)
        {
            var option = mapper(item);
            if (option.IsNone)
                return Option<IReadOnlyList<TResult>>.None();

            results.Add(option.Value);
        }

        return Option<IReadOnlyList<TResult>>.Some(results);
    }

    /// <summary>
    /// Finds the first option that has a value.
    /// </summary>
    /// <typeparam name="T">The type of the option values</typeparam>
    /// <param name="options">The options to search</param>
    /// <returns>The first option with a value, or None if all are empty</returns>
    public static Option<T> FirstSome<T>(IEnumerable<Option<T>> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        foreach (var option in options)
        {
            if (option.HasValue)
                return option;
        }

        return Option<T>.None();
    }

    /// <summary>
    /// Finds the first option that has a value.
    /// </summary>
    /// <typeparam name="T">The type of the option values</typeparam>
    /// <param name="options">The options to search</param>
    /// <returns>The first option with a value, or None if all are empty</returns>
    public static Option<T> FirstSome<T>(params Option<T>[] options)
    {
        return FirstSome(options.AsEnumerable());
    }
}
