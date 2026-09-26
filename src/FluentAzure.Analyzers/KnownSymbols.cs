using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace FluentAzure.Analyzers
{
    /// <summary>
    /// Helpers for recognising FluentAzure APIs in operations.
    /// </summary>
    internal static class KnownSymbols
    {
        public const string PipelineTypeName = "FluentAzure.Core.ConfigurationBuilder";

        public static bool IsPipelineMethod(IMethodSymbol method)
        {
            var receiverType = method.IsExtensionMethod
                ? (method.ReducedFrom ?? method).Parameters.Length > 0
                    ? (method.ReducedFrom ?? method).Parameters[0].Type
                    : null
                : method.ContainingType;
            return receiverType?.ToDisplayString() == PipelineTypeName;
        }

        public static bool IsInFluentAzureNamespace(ISymbol symbol)
        {
            var ns = symbol.ContainingNamespace?.ToDisplayString();
            return ns != null && (ns == "FluentAzure" || ns.StartsWith("FluentAzure.", System.StringComparison.Ordinal));
        }

        /// <summary>
        /// Gets the receiver of a call, whether it is an instance call or a reduced extension call.
        /// </summary>
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
