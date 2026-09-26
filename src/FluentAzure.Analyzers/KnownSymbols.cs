using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace FluentAzure.Analyzers
{
    /// <summary>
    /// Helpers for recognising FluentAzure APIs in operations.
    /// </summary>
    internal static class KnownSymbols
    {
        /// <summary>
        /// The full name of the FluentAzure pipeline type.
        /// </summary>
        public const string PipelineTypeName = "FluentAzure.Core.ConfigurationBuilder";

        /// <summary>
        /// Determines whether a method is an instance or extension method of the FluentAzure pipeline.
        /// </summary>
        /// <param name="method">The method to check.</param>
        /// <returns><see langword="true"/> if the method is called on the pipeline.</returns>
        public static bool IsPipelineMethod(IMethodSymbol method)
        {
            var receiverType = method.IsExtensionMethod
                ? (method.ReducedFrom ?? method).Parameters.Length > 0
                    ? (method.ReducedFrom ?? method).Parameters[0].Type
                    : null
                : method.ContainingType;
            return receiverType?.ToDisplayString() == PipelineTypeName;
        }

        /// <summary>
        /// Determines whether a symbol is declared in the <c>FluentAzure</c> namespace or one of its children.
        /// </summary>
        /// <param name="symbol">The symbol to check.</param>
        /// <returns><see langword="true"/> if the symbol belongs to FluentAzure.</returns>
        public static bool IsInFluentAzureNamespace(ISymbol symbol)
        {
            var ns = symbol.ContainingNamespace?.ToDisplayString();
            return ns != null && (ns == "FluentAzure" || ns.StartsWith("FluentAzure.", System.StringComparison.Ordinal));
        }

        /// <summary>
        /// Gets the receiver of a call, whether it is an instance call or a reduced extension call.
        /// </summary>
        /// <param name="invocation">The call.</param>
        /// <returns>The receiver, or <see langword="null"/> for a static call.</returns>
        public static IOperation? GetReceiver(IInvocationOperation invocation)
        {
            if (invocation.Instance != null)
            {
                return Unwrap(invocation.Instance);
            }

            if (invocation.TargetMethod.IsExtensionMethod && invocation.Arguments.Length > 0)
            {
                return Unwrap(invocation.Arguments[0].Value);
            }

            return null;
        }

        /// <summary>
        /// Gets the constant string value of the argument bound to the named parameter, if any.
        /// </summary>
        /// <param name="invocation">The call.</param>
        /// <param name="parameterName">The name of the parameter.</param>
        /// <param name="value">The constant string value, if found.</param>
        /// <param name="argument">The argument, if found.</param>
        /// <returns><see langword="true"/> if the argument is a constant string.</returns>
        public static bool TryGetStringArgument(IInvocationOperation invocation, string parameterName, out string value, out IArgumentOperation? argument)
        {
            foreach (var candidate in invocation.Arguments)
            {
                if (candidate.Parameter?.Name == parameterName
                    && candidate.Value.ConstantValue.HasValue
                    && candidate.Value.ConstantValue.Value is string text)
                {
                    value = text;
                    argument = candidate;
                    return true;
                }
            }

            value = string.Empty;
            argument = null;
            return false;
        }

        /// <summary>
        /// Gets the constant string value of the constructor argument at the given position, if any.
        /// </summary>
        /// <param name="creation">The object creation.</param>
        /// <param name="ordinal">The position of the constructor parameter.</param>
        /// <param name="value">The constant string value, if found.</param>
        /// <param name="argument">The argument, if found.</param>
        /// <returns><see langword="true"/> if the argument is a constant string.</returns>
        public static bool TryGetStringArgument(IObjectCreationOperation creation, int ordinal, out string value, out IArgumentOperation? argument)
        {
            foreach (var candidate in creation.Arguments)
            {
                if (candidate.Parameter?.Ordinal == ordinal
                    && candidate.Parameter.Type.SpecialType == SpecialType.System_String
                    && candidate.Value.ConstantValue.HasValue
                    && candidate.Value.ConstantValue.Value is string text)
                {
                    value = text;
                    argument = candidate;
                    return true;
                }
            }

            value = string.Empty;
            argument = null;
            return false;
        }

        private static IOperation Unwrap(IOperation operation)
        {
            while (operation is IConversionOperation conversion)
            {
                operation = conversion.Operand;
            }

            return operation;
        }
    }
}
