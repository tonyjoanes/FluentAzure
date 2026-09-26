using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace FluentAzure.Analyzers
{
    /// <summary>
    /// FAZ0001: in a fluent chain ending with BuildAsync&lt;T&gt;() or BuildOptionalAsync&lt;T&gt;(), reports
    /// Required("key") calls whose key does not correspond to any property path of T.
    /// </summary>
    /// <remarks>
    /// Matching is deliberately lenient to avoid false positives: separators (":", "__", "_", "-", ".") and case are
    /// ignored, and anything below a dictionary, collection, object or IConfiguration-typed member is accepted.
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class RequiredKeyNotBoundAnalyzer : DiagnosticAnalyzer
    {
        private const int MaxDepth = 6;

        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Diagnostics.RequiredKeyNotBound);

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
        }

        private static void AnalyzeInvocation(OperationAnalysisContext context)
        {
            var build = (IInvocationOperation)context.Operation;
            var method = build.TargetMethod;
            if ((method.Name != "BuildAsync" && method.Name != "BuildOptionalAsync")
                || method.TypeArguments.Length != 1
                || !KnownSymbols.IsPipelineMethod(method)
                || !(method.TypeArguments[0] is INamedTypeSymbol targetType)
                || targetType.TypeKind == TypeKind.TypeParameter)
            {
                return;
            }

            var paths = CollectPropertyPaths(targetType);

            // Walk the fluent chain backwards from BuildAsync<T>() looking for Required("...") calls
            var receiver = KnownSymbols.GetReceiver(build);
            while (receiver is IInvocationOperation call && KnownSymbols.IsPipelineMethod(call.TargetMethod))
            {
                if (call.TargetMethod.Name == "Required"
                    && KnownSymbols.TryGetStringArgument(call, "key", out var key, out var argument)
                    && key.Length > 0
                    && !Matches(Normalize(key), paths))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        Diagnostics.RequiredKeyNotBound,
                        argument!.Syntax.GetLocation(),
                        key,
                        targetType.Name));
                }

                receiver = KnownSymbols.GetReceiver(call);
            }
        }

        private static bool Matches(string normalizedKey, List<(string Path, bool OpenEnded)> paths)
        {
            foreach (var (path, openEnded) in paths)
            {
                if (normalizedKey == path || (openEnded && normalizedKey.StartsWith(path, StringComparison.Ordinal)))
                {
                    return true;
                }
            }

            return false;
        }

        private static List<(string Path, bool OpenEnded)> CollectPropertyPaths(INamedTypeSymbol root)
        {
            var paths = new List<(string Path, bool OpenEnded)>();
            Collect(root, string.Empty, 0, paths, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));
            return paths;
        }

        private static void Collect(ITypeSymbol type, string prefix, int depth, List<(string Path, bool OpenEnded)> paths, HashSet<ITypeSymbol> visiting)
        {
            if (depth > MaxDepth || !visiting.Add(type))
            {
                // Cyclic or very deep types: accept anything below this point
                if (prefix.Length > 0)
                {
                    paths.Add((prefix, true));
                }

                return;
            }

            foreach (var member in GetBindableMembers(type))
            {
                var path = prefix + Normalize(member.Name);
                var memberType = member.Type;

                if (IsOpenEnded(memberType))
                {
                    paths.Add((path, true));
                }
                else if (IsSimple(memberType))
                {
                    paths.Add((path, false));
                }
                else
                {
                    // A complex member is itself a valid key (e.g. a JSON value), and so are its children
                    paths.Add((path, false));
                    Collect(memberType, path, depth + 1, paths, visiting);
                }
            }

            visiting.Remove(type);
        }

        private static IEnumerable<(string Name, ITypeSymbol Type)> GetBindableMembers(ITypeSymbol type)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var current = type; current != null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
            {
                foreach (var member in current.GetMembers())
                {
                    if (member is IPropertySymbol property
                        && !property.IsStatic
                        && !property.IsIndexer
                        && property.DeclaredAccessibility == Accessibility.Public
                        && seen.Add(property.Name))
                    {
                        yield return (property.Name, property.Type);
                    }
                }
            }

            // Constructor parameters (records, immutable types) are bindable too
            if (type is INamedTypeSymbol named)
            {
                foreach (var constructor in named.InstanceConstructors.Where(c => c.DeclaredAccessibility == Accessibility.Public))
                {
                    foreach (var parameter in constructor.Parameters)
                    {
                        if (seen.Add(parameter.Name))
                        {
                            yield return (parameter.Name, parameter.Type);
                        }
                    }
                }
            }
        }

        private static bool IsSimple(ITypeSymbol type)
        {
            if (type is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                type = named.TypeArguments[0];
            }

            if (type.TypeKind == TypeKind.Enum || type.SpecialType != SpecialType.None)
            {
                return type.SpecialType != SpecialType.System_Object;
            }

            var name = type.ToDisplayString();
            return name == "System.Guid" || name == "System.Uri" || name == "System.TimeSpan"
                || name == "System.DateTimeOffset" || name == "System.DateOnly" || name == "System.TimeOnly";
        }

        private static bool IsOpenEnded(ITypeSymbol type)
        {
            if (type.SpecialType == SpecialType.System_Object || type.TypeKind == TypeKind.Array || type.TypeKind == TypeKind.Dynamic)
            {
                return true;
            }

            if (type.SpecialType == SpecialType.System_String)
            {
                return false;
            }

            var name = type.OriginalDefinition.ToDisplayString();
            if (name.StartsWith("Microsoft.Extensions.Configuration.", StringComparison.Ordinal))
            {
                return true;
            }

            // Dictionaries and collections accept arbitrary child keys (names or indexes)
            return type.AllInterfaces.Any(i => i.OriginalDefinition.ToDisplayString() == "System.Collections.IEnumerable")
                || name == "System.Collections.IEnumerable";
        }

        private static string Normalize(string text)
        {
            var builder = new StringBuilder(text.Length);
            foreach (var c in text)
            {
                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(char.ToLowerInvariant(c));
                }
            }

            return builder.ToString();
        }
    }
}
