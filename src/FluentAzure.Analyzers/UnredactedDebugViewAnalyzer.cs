using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace FluentAzure.Analyzers
{
    /// <summary>
    /// FAZ0004: reports IConfigurationRoot.GetDebugView() without a value processor, which prints secret values.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class UnredactedDebugViewAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Diagnostics.UnredactedDebugView);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(start =>
            {
                // Only relevant when the project actually uses FluentAzure's IConfiguration integration
                if (start.Compilation.GetTypeByMetadataName("FluentAzure.Configuration.FluentAzureConfigurationProvider") == null)
                {
                    return;
                }

                start.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
            });
        }

        private static void AnalyzeInvocation(OperationAnalysisContext context)
        {
            var invocation = (IInvocationOperation)context.Operation;
            var method = invocation.TargetMethod.ReducedFrom ?? invocation.TargetMethod;
            if (method.Name == "GetDebugView"
                && method.Parameters.Length == 1
                && method.ContainingType.ToDisplayString() == "Microsoft.Extensions.Configuration.ConfigurationRootExtensions")
            {
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.UnredactedDebugView, invocation.Syntax.GetLocation()));
            }
        }
    }
}
