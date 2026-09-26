using System.Diagnostics.CodeAnalysis;
using FluentAzure.Binding;
using FluentAzure.Core;
using Microsoft.Extensions.DependencyInjection;

namespace FluentAzure;

/// <summary>
/// Extension methods for binding configuration results.
/// These are available directly when using FluentAzure.
/// </summary>
public static class BindingExtensions
{
    /// <summary>
    /// Binds configuration to a strongly-typed object.
    /// </summary>
    /// <typeparam name="T">The type to bind to.</typeparam>
    /// <param name="result">The configuration result.</param>
    /// <returns>A result containing the bound object or errors.</returns>
    [RequiresUnreferencedCode(AotMessages.ReflectionBinding)]
    [RequiresDynamicCode(AotMessages.ReflectionBinding)]
    public static Result<T> Bind<T>(this Result<Dictionary<string, string>> result)
        where T : class, new()
    {
        return result.Bind(config => ConfigurationBinder.Bind<T>(config));
    }
}
