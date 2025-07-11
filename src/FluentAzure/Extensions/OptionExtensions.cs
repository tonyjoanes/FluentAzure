using FluentAzure.Core;

namespace FluentAzure.Extensions;

/// <summary>
/// Extension methods for integrating Option&lt;T&gt; with FluentAzure configuration.
/// </summary>
public static class OptionExtensions
{
    /// <summary>
    /// Converts a Result to an Option, discarding error information.
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <param name="result">The result to convert</param>
    /// <returns>Some(value) if the result is successful, None otherwise</returns>
    public static Option<T> ToOption<T>(this Result<T> result)
    {
        return result.IsSuccess ? Option<T>.Some(result.Value) : Option<T>.None();
    }

    /// <summary>
    /// Gets a configuration value as an Option.
    /// </summary>
    /// <param name="config">The configuration dictionary</param>
    /// <param name="key">The configuration key</param>
    /// <returns>Some(value) if the key exists, None otherwise</returns>
    public static Option<string> GetOptional(this Dictionary<string, string> config, string key)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(key);

        return config.TryGetValue(key, out var value)
            ? Option<string>.Some(value)
            : Option<string>.None();
    }

    /// <summary>
    /// Gets a configuration value as an Option with type conversion.
    /// </summary>
    /// <typeparam name="T">The type to convert to</typeparam>
    /// <param name="config">The configuration dictionary</param>
    /// <param name="key">The configuration key</param>
    /// <returns>Some(converted value) if the key exists and conversion succeeds, None otherwise</returns>
    public static Option<T> GetOptional<T>(this Dictionary<string, string> config, string key)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(key);

        if (!config.TryGetValue(key, out var value))
            return Option<T>.None();

        try
        {
            var converted = Convert.ChangeType(value, typeof(T));
            return converted is T typedValue ? Option<T>.Some(typedValue) : Option<T>.None();
        }
        catch
        {
            return Option<T>.None();
        }
    }

    /// <summary>
    /// Gets a configuration value as an Option with custom parser.
    /// </summary>
    /// <typeparam name="T">The type to parse to</typeparam>
    /// <param name="config">The configuration dictionary</param>
    /// <param name="key">The configuration key</param>
    /// <param name="parser">The parser function</param>
    /// <returns>Some(parsed value) if the key exists and parsing succeeds, None otherwise</returns>
    public static Option<T> GetOptional<T>(
        this Dictionary<string, string> config,
        string key,
        Func<string, T> parser
    )
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(parser);

        if (!config.TryGetValue(key, out var value))
            return Option<T>.None();

        try
        {
            var parsed = parser(value);
            return Option<T>.Some(parsed);
        }
        catch
        {
            return Option<T>.None();
        }
    }

    /// <summary>
    /// Gets a configuration value as an Option with try-parse pattern.
    /// </summary>
    /// <typeparam name="T">The type to parse to</typeparam>
    /// <param name="config">The configuration dictionary</param>
    /// <param name="key">The configuration key</param>
    /// <param name="tryParser">The try-parse function</param>
    /// <returns>Some(parsed value) if the key exists and parsing succeeds, None otherwise</returns>
    public static Option<T> GetOptional<T>(
        this Dictionary<string, string> config,
        string key,
        TryParse<T> tryParser
    )
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(tryParser);

        if (!config.TryGetValue(key, out var value))
            return Option<T>.None();

        return tryParser(value, out var result) ? Option<T>.Some(result) : Option<T>.None();
    }

    /// <summary>
    /// Maps over a collection of options, keeping only the Some values.
    /// </summary>
    /// <typeparam name="T">The type of the option values</typeparam>
    /// <param name="options">The collection of options</param>
    /// <returns>A collection containing only the Some values</returns>
    public static IEnumerable<T> Choose<T>(this IEnumerable<Option<T>> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Where(o => o.HasValue).Select(o => o.Value);
    }

    /// <summary>
    /// Maps a function over a collection, keeping only the Some results.
    /// </summary>
    /// <typeparam name="T">The type of the input elements</typeparam>
    /// <typeparam name="TResult">The type of the result elements</typeparam>
    /// <param name="items">The collection of items</param>
    /// <param name="mapper">The function to map over each item</param>
    /// <returns>A collection containing only the Some results</returns>
    public static IEnumerable<TResult> Choose<T, TResult>(
        this IEnumerable<T> items,
        Func<T, Option<TResult>> mapper
    )
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(mapper);

        return items.Select(mapper).Choose();
    }

    /// <summary>
    /// Finds the first Some value in a collection of options.
    /// </summary>
    /// <typeparam name="T">The type of the option values</typeparam>
    /// <param name="options">The collection of options</param>
    /// <returns>The first Some value, or None if all are None</returns>
    public static Option<T> FirstSome<T>(this IEnumerable<Option<T>> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return Option.FirstSome(options);
    }

    /// <summary>
    /// Partitions a collection of options into Some and None values.
    /// </summary>
    /// <typeparam name="T">The type of the option values</typeparam>
    /// <param name="options">The collection of options</param>
    /// <returns>A tuple containing the Some values and the count of None values</returns>
    public static (IReadOnlyList<T> Some, int NoneCount) Partition<T>(
        this IEnumerable<Option<T>> options
    )
    {
        ArgumentNullException.ThrowIfNull(options);

        var someValues = new List<T>();
        var noneCount = 0;

        foreach (var option in options)
        {
            if (option.HasValue)
                someValues.Add(option.Value);
            else
                noneCount++;
        }

        return (someValues, noneCount);
    }

    /// <summary>
    /// Applies a function to an option value if it satisfies a predicate.
    /// </summary>
    /// <typeparam name="T">The type of the option value</typeparam>
    /// <param name="option">The option</param>
    /// <param name="predicate">The predicate to test</param>
    /// <param name="action">The action to apply if the predicate is true</param>
    /// <returns>The original option for chaining</returns>
    public static Option<T> DoIf<T>(
        this Option<T> option,
        Func<T, bool> predicate,
        Action<T> action
    )
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(action);

        if (option.HasValue && predicate(option.Value))
            action(option.Value);

        return option;
    }

    /// <summary>
    /// Applies a function to an option value if it satisfies a predicate.
    /// Alias for DoIf to match common FP terminology.
    /// </summary>
    /// <typeparam name="T">The type of the option value</typeparam>
    /// <param name="option">The option</param>
    /// <param name="predicate">The predicate to test</param>
    /// <param name="action">The action to apply if the predicate is true</param>
    /// <returns>The original option for chaining</returns>
    public static Option<T> TapIf<T>(
        this Option<T> option,
        Func<T, bool> predicate,
        Action<T> action
    ) => option.DoIf(predicate, action);

    /// <summary>
    /// Flattens a nested option.
    /// </summary>
    /// <typeparam name="T">The type of the inner option value</typeparam>
    /// <param name="option">The nested option</param>
    /// <returns>The flattened option</returns>
    public static Option<T> Flatten<T>(this Option<Option<T>> option)
    {
        return option.HasValue ? option.Value : Option<T>.None();
    }

    /// <summary>
    /// Converts an option to a single-element or empty enumerable.
    /// </summary>
    /// <typeparam name="T">The type of the option value</typeparam>
    /// <param name="option">The option</param>
    /// <returns>An enumerable with the value if Some, empty if None</returns>
    public static IEnumerable<T> AsEnumerable<T>(this Option<T> option)
    {
        return option.ToEnumerable();
    }

    /// <summary>
    /// Tries to get a value from a dictionary as an Option.
    /// </summary>
    /// <typeparam name="TKey">The type of the dictionary keys</typeparam>
    /// <typeparam name="TValue">The type of the dictionary values</typeparam>
    /// <param name="dictionary">The dictionary</param>
    /// <param name="key">The key to look up</param>
    /// <returns>Some(value) if the key exists, None otherwise</returns>
    public static Option<TValue> TryGetValue<TKey, TValue>(
        this Dictionary<TKey, TValue> dictionary,
        TKey key
    )
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        ArgumentNullException.ThrowIfNull(key);

        return dictionary.TryGetValue(key, out var value)
            ? Option<TValue>.Some(value)
            : Option<TValue>.None();
    }

    /// <summary>
    /// Tries to get a value from a read-only dictionary as an Option.
    /// </summary>
    /// <typeparam name="TKey">The type of the dictionary keys</typeparam>
    /// <typeparam name="TValue">The type of the dictionary values</typeparam>
    /// <param name="dictionary">The dictionary</param>
    /// <param name="key">The key to look up</param>
    /// <returns>Some(value) if the key exists, None otherwise</returns>
    public static Option<TValue> TryGetValue<TKey, TValue>(
        this IReadOnlyDictionary<TKey, TValue> dictionary,
        TKey key
    )
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        ArgumentNullException.ThrowIfNull(key);

        return dictionary.TryGetValue(key, out var value)
            ? Option<TValue>.Some(value)
            : Option<TValue>.None();
    }

    /// <summary>
    /// Safely gets an element from a list at the specified index.
    /// </summary>
    /// <typeparam name="T">The type of the list elements</typeparam>
    /// <param name="list">The list</param>
    /// <param name="index">The index to access</param>
    /// <returns>Some(element) if the index is valid, None otherwise</returns>
    public static Option<T> ElementAtOrNone<T>(this IReadOnlyList<T> list, int index)
    {
        ArgumentNullException.ThrowIfNull(list);

        return index >= 0 && index < list.Count ? Option<T>.Some(list[index]) : Option<T>.None();
    }

    /// <summary>
    /// Safely gets the first element from a collection.
    /// </summary>
    /// <typeparam name="T">The type of the collection elements</typeparam>
    /// <param name="collection">The collection</param>
    /// <returns>Some(first element) if the collection is not empty, None otherwise</returns>
    public static Option<T> FirstOrNone<T>(this IEnumerable<T> collection)
    {
        ArgumentNullException.ThrowIfNull(collection);

        foreach (var item in collection)
            return Option<T>.Some(item);

        return Option<T>.None();
    }

    /// <summary>
    /// Safely gets the first element from a collection that satisfies a predicate.
    /// </summary>
    /// <typeparam name="T">The type of the collection elements</typeparam>
    /// <param name="collection">The collection</param>
    /// <param name="predicate">The predicate to test</param>
    /// <returns>Some(first matching element) if found, None otherwise</returns>
    public static Option<T> FirstOrNone<T>(this IEnumerable<T> collection, Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(predicate);

        foreach (var item in collection)
        {
            if (predicate(item))
                return Option<T>.Some(item);
        }

        return Option<T>.None();
    }

    /// <summary>
    /// Safely gets the last element from a collection.
    /// </summary>
    /// <typeparam name="T">The type of the collection elements</typeparam>
    /// <param name="collection">The collection</param>
    /// <returns>Some(last element) if the collection is not empty, None otherwise</returns>
    public static Option<T> LastOrNone<T>(this IEnumerable<T> collection)
    {
        ArgumentNullException.ThrowIfNull(collection);

        var hasValue = false;
        var lastValue = default(T);

        foreach (var item in collection)
        {
            hasValue = true;
            lastValue = item;
        }

        return hasValue ? Option<T>.Some(lastValue!) : Option<T>.None();
    }

    /// <summary>
    /// Safely gets the single element from a collection.
    /// </summary>
    /// <typeparam name="T">The type of the collection elements</typeparam>
    /// <param name="collection">The collection</param>
    /// <returns>Some(single element) if the collection contains exactly one element, None otherwise</returns>
    public static Option<T> SingleOrNone<T>(this IEnumerable<T> collection)
    {
        ArgumentNullException.ThrowIfNull(collection);

        using var enumerator = collection.GetEnumerator();

        if (!enumerator.MoveNext())
            return Option<T>.None();

        var value = enumerator.Current;

        if (enumerator.MoveNext())
            return Option<T>.None(); // More than one element

        return Option<T>.Some(value);
    }

    /// <summary>
    /// Safely gets the single element from a collection that satisfies a predicate.
    /// </summary>
    /// <typeparam name="T">The type of the collection elements</typeparam>
    /// <param name="collection">The collection</param>
    /// <param name="predicate">The predicate to test</param>
    /// <returns>Some(single matching element) if exactly one element matches, None otherwise</returns>
    public static Option<T> SingleOrNone<T>(this IEnumerable<T> collection, Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(predicate);

        var found = false;
        var result = default(T);

        foreach (var item in collection)
        {
            if (predicate(item))
            {
                if (found)
                    return Option<T>.None(); // More than one match

                found = true;
                result = item;
            }
        }

        return found ? Option<T>.Some(result!) : Option<T>.None();
    }

    /// <summary>
    /// Converts a nullable value to an Option.
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <param name="value">The nullable value</param>
    /// <returns>Some(value) if not null, None otherwise</returns>
    public static Option<T> ToOption<T>(this T? value)
        where T : class
    {
        return Option<T>.FromNullable(value);
    }

    /// <summary>
    /// Converts a nullable value type to an Option.
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <param name="value">The nullable value</param>
    /// <returns>Some(value) if not null, None otherwise</returns>
    public static Option<T> ToOption<T>(this T? value)
        where T : struct
    {
        return value.HasValue ? Option<T>.Some(value.Value) : Option<T>.None();
    }

    /// <summary>
    /// Converts a boolean condition to an Option with a value.
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <param name="condition">The condition</param>
    /// <param name="value">The value to wrap if condition is true</param>
    /// <returns>Some(value) if condition is true, None otherwise</returns>
    public static Option<T> ToOption<T>(this bool condition, T value)
    {
        return condition ? Option<T>.Some(value) : Option<T>.None();
    }

    /// <summary>
    /// Converts a boolean condition to an Option with a factory function.
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <param name="condition">The condition</param>
    /// <param name="valueFactory">The factory function to create the value if condition is true</param>
    /// <returns>Some(value) if condition is true, None otherwise</returns>
    public static Option<T> ToOption<T>(this bool condition, Func<T> valueFactory)
    {
        ArgumentNullException.ThrowIfNull(valueFactory);
        return condition ? Option<T>.Some(valueFactory()) : Option<T>.None();
    }

    /// <summary>
    /// Validates an option value using a predicate.
    /// </summary>
    /// <typeparam name="T">The type of the option value</typeparam>
    /// <param name="option">The option</param>
    /// <param name="validator">The validation predicate</param>
    /// <param name="errorMessage">The error message if validation fails</param>
    /// <returns>Success with the value if valid, Error otherwise</returns>
    public static Result<T> Validate<T>(
        this Option<T> option,
        Func<T, bool> validator,
        string errorMessage
    )
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(errorMessage);

        return option.Match(
            some: value =>
                validator(value) ? Result<T>.Success(value) : Result<T>.Error(errorMessage),
            none: () => Result<T>.Error("Option is None")
        );
    }

    /// <summary>
    /// Validates an option value using a predicate with custom error factory.
    /// </summary>
    /// <typeparam name="T">The type of the option value</typeparam>
    /// <param name="option">The option</param>
    /// <param name="validator">The validation predicate</param>
    /// <param name="errorFactory">The error message factory</param>
    /// <returns>Success with the value if valid, Error otherwise</returns>
    public static Result<T> Validate<T>(
        this Option<T> option,
        Func<T, bool> validator,
        Func<T, string> errorFactory
    )
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(errorFactory);

        return option.Match(
            some: value =>
                validator(value) ? Result<T>.Success(value) : Result<T>.Error(errorFactory(value)),
            none: () => Result<T>.Error("Option is None")
        );
    }

    /// <summary>
    /// Maps an option to another option using an async function.
    /// </summary>
    /// <typeparam name="T">The type of the input value</typeparam>
    /// <typeparam name="TResult">The type of the result value</typeparam>
    /// <param name="option">The option</param>
    /// <param name="mapper">The async mapping function</param>
    /// <returns>A task that completes with the mapped option</returns>
    public static async Task<Option<TResult>> MapAsync<T, TResult>(
        this Option<T> option,
        Func<T, Task<TResult>> mapper
    )
    {
        ArgumentNullException.ThrowIfNull(mapper);

        return await option.Match(
            some: async value => Option<TResult>.Some(await mapper(value)),
            none: () => Task.FromResult(Option<TResult>.None())
        );
    }

    /// <summary>
    /// Binds an option to another option using an async function.
    /// </summary>
    /// <typeparam name="T">The type of the input value</typeparam>
    /// <typeparam name="TResult">The type of the result value</typeparam>
    /// <param name="option">The option</param>
    /// <param name="binder">The async binding function</param>
    /// <returns>A task that completes with the bound option</returns>
    public static async Task<Option<TResult>> BindAsync<T, TResult>(
        this Option<T> option,
        Func<T, Task<Option<TResult>>> binder
    )
    {
        ArgumentNullException.ThrowIfNull(binder);

        return await option.Match(
            some: async value => await binder(value),
            none: () => Task.FromResult(Option<TResult>.None())
        );
    }

    /// <summary>
    /// Executes an async action on an option value if present.
    /// </summary>
    /// <typeparam name="T">The type of the option value</typeparam>
    /// <param name="option">The option</param>
    /// <param name="action">The async action to execute</param>
    /// <returns>A task that completes when the action is executed</returns>
    public static async Task DoAsync<T>(this Option<T> option, Func<T, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        await option.Match(
            some: async value => await action(value),
            none: () => Task.CompletedTask
        );
    }

    /// <summary>
    /// Converts an option to a task that completes with the value or throws an exception.
    /// </summary>
    /// <typeparam name="T">The type of the option value</typeparam>
    /// <param name="option">The option</param>
    /// <param name="exceptionFactory">The factory for creating the exception if the option is None</param>
    /// <returns>A task that completes with the value or throws an exception</returns>
    public static async Task<T> ToTask<T>(this Option<T> option, Func<Exception> exceptionFactory)
    {
        ArgumentNullException.ThrowIfNull(exceptionFactory);

        return await option.Match(
            some: value => Task.FromResult(value),
            none: () => Task.FromException<T>(exceptionFactory())
        );
    }

    /// <summary>
    /// Converts an option to a task that completes with the value or throws a default exception.
    /// </summary>
    /// <typeparam name="T">The type of the option value</typeparam>
    /// <param name="option">The option</param>
    /// <param name="errorMessage">The error message for the exception</param>
    /// <returns>A task that completes with the value or throws an exception</returns>
    public static Task<T> ToTask<T>(this Option<T> option, string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(errorMessage);
        return option.ToTask(() => new InvalidOperationException(errorMessage));
    }

    /// <summary>
    /// Combines two options using a function.
    /// </summary>
    /// <typeparam name="T1">The type of the first option value</typeparam>
    /// <typeparam name="T2">The type of the second option value</typeparam>
    /// <typeparam name="TResult">The type of the result</typeparam>
    /// <param name="option1">The first option</param>
    /// <param name="option2">The second option</param>
    /// <param name="combiner">The function to combine the values</param>
    /// <returns>Some(combined result) if both options have values, None otherwise</returns>
    public static Option<TResult> Combine<T1, T2, TResult>(
        this Option<T1> option1,
        Option<T2> option2,
        Func<T1, T2, TResult> combiner
    )
    {
        ArgumentNullException.ThrowIfNull(combiner);

        return option1.HasValue && option2.HasValue
            ? Option<TResult>.Some(combiner(option1.Value, option2.Value))
            : Option<TResult>.None();
    }

    /// <summary>
    /// Combines three options using a function.
    /// </summary>
    /// <typeparam name="T1">The type of the first option value</typeparam>
    /// <typeparam name="T2">The type of the second option value</typeparam>
    /// <typeparam name="T3">The type of the third option value</typeparam>
    /// <typeparam name="TResult">The type of the result</typeparam>
    /// <param name="option1">The first option</param>
    /// <param name="option2">The second option</param>
    /// <param name="option3">The third option</param>
    /// <param name="combiner">The function to combine the values</param>
    /// <returns>Some(combined result) if all options have values, None otherwise</returns>
    public static Option<TResult> Combine<T1, T2, T3, TResult>(
        this Option<T1> option1,
        Option<T2> option2,
        Option<T3> option3,
        Func<T1, T2, T3, TResult> combiner
    )
    {
        ArgumentNullException.ThrowIfNull(combiner);

        return option1.HasValue && option2.HasValue && option3.HasValue
            ? Option<TResult>.Some(combiner(option1.Value, option2.Value, option3.Value))
            : Option<TResult>.None();
    }

    /// <summary>
    /// Applies a function to an option value and returns the result as an option.
    /// </summary>
    /// <typeparam name="T">The type of the option value</typeparam>
    /// <typeparam name="TResult">The type of the result</typeparam>
    /// <param name="option">The option</param>
    /// <param name="func">The function to apply</param>
    /// <returns>Some(result) if the option has a value, None otherwise</returns>
    public static Option<TResult> Apply<T, TResult>(this Option<T> option, Func<T, TResult> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        return option.Map(func);
    }

    /// <summary>
    /// Applies a function that returns an option to an option value.
    /// </summary>
    /// <typeparam name="T">The type of the option value</typeparam>
    /// <typeparam name="TResult">The type of the result</typeparam>
    /// <param name="option">The option</param>
    /// <param name="func">The function that returns an option</param>
    /// <returns>The result of applying the function</returns>
    public static Option<TResult> Apply<T, TResult>(
        this Option<T> option,
        Func<T, Option<TResult>> func
    )
    {
        ArgumentNullException.ThrowIfNull(func);
        return option.Bind(func);
    }



    /// <summary>
    /// Gets the value or throws an exception if the option is None.
    /// </summary>
    /// <typeparam name="T">The type of the option value</typeparam>
    /// <param name="option">The option</param>
    /// <param name="exceptionFactory">The factory for creating the exception</param>
    /// <returns>The value if present</returns>
    /// <exception cref="Exception">Thrown when the option is None</exception>
    public static T GetValueOrThrow<T>(this Option<T> option, Func<Exception> exceptionFactory)
    {
        ArgumentNullException.ThrowIfNull(exceptionFactory);

        return option.Match(some: value => value, none: () => throw exceptionFactory());
    }

    /// <summary>
    /// Gets the value or throws an InvalidOperationException if the option is None.
    /// </summary>
    /// <typeparam name="T">The type of the option value</typeparam>
    /// <param name="option">The option</param>
    /// <param name="errorMessage">The error message</param>
    /// <returns>The value if present</returns>
    /// <exception cref="InvalidOperationException">Thrown when the option is None</exception>
    public static T GetValueOrThrow<T>(this Option<T> option, string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(errorMessage);
        return option.GetValueOrThrow(() => new InvalidOperationException(errorMessage));
    }

    /// <summary>
    /// Gets the value or throws a default InvalidOperationException if the option is None.
    /// </summary>
    /// <typeparam name="T">The type of the option value</typeparam>
    /// <param name="option">The option</param>
    /// <returns>The value if present</returns>
    /// <exception cref="InvalidOperationException">Thrown when the option is None</exception>
    public static T GetValueOrThrow<T>(this Option<T> option)
    {
        return option.GetValueOrThrow("Option is None");
    }
}

/// <summary>
/// Delegate for try-parse functions.
/// </summary>
/// <typeparam name="T">The type to parse to</typeparam>
/// <param name="input">The input string</param>
/// <param name="result">The parsed result</param>
/// <returns>True if parsing succeeded, false otherwise</returns>
public delegate bool TryParse<T>(string input, out T result);
