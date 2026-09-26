using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace FluentAzure.Analyzers
{
    /// <summary>
    /// FAZ0002: Key Vault / App Configuration endpoints given as literals must be absolute HTTPS URIs.
    /// FAZ0003: App Configuration connection strings containing an access key secret must not be hard-coded.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class AzureEndpointAnalyzer : DiagnosticAnalyzer
    {
        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Diagnostics.InsecureEndpoint, Diagnostics.HardcodedConnectionStringSecret);

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
            context.RegisterOperationAction(AnalyzeObjectCreation, OperationKind.ObjectCreation);
        }

        private static void AnalyzeInvocation(OperationAnalysisContext context)
        {
            var invocation = (IInvocationOperation)context.Operation;
            var method = invocation.TargetMethod;
            if (!KnownSymbols.IsPipelineMethod(method))
            {
                return;
            }

            if (method.Name.StartsWith("FromKeyVault", StringComparison.Ordinal)
                && KnownSymbols.TryGetStringArgument(invocation, "vaultUrl", out var vaultUrl, out var vaultArgument))
            {
                CheckEndpoint(context, vaultUrl, vaultArgument!, "Key Vault");
            }
            else if (method.Name == "FromAppConfiguration"
                && KnownSymbols.TryGetStringArgument(invocation, "endpointOrConnectionString", out var target, out var targetArgument))
            {
                CheckAppConfiguration(context, target, targetArgument!);
            }
        }

        private static void AnalyzeObjectCreation(OperationAnalysisContext context)
        {
            var creation = (IObjectCreationOperation)context.Operation;
            var typeName = creation.Type?.ToDisplayString();
            if (typeName == "FluentAzure.Sources.KeyVaultSource"
                && KnownSymbols.TryGetStringArgument(creation, 0, out var vaultUrl, out var vaultArgument))
            {
                CheckEndpoint(context, vaultUrl, vaultArgument!, "Key Vault");
            }
            else if (typeName == "FluentAzure.Sources.AppConfigurationSource"
                && KnownSymbols.TryGetStringArgument(creation, 0, out var connectionString, out var connectionArgument))
            {
                CheckAppConfiguration(context, connectionString, connectionArgument!);
            }
        }

        private static void CheckAppConfiguration(OperationAnalysisContext context, string value, IArgumentOperation argument)
        {
            if (value.IndexOf("Secret=", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.HardcodedConnectionStringSecret, argument.Syntax.GetLocation()));
            }
            else if (value.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                CheckEndpoint(context, value, argument, "App Configuration");
            }
        }

        private static void CheckEndpoint(OperationAnalysisContext context, string value, IArgumentOperation argument, string service)
        {
            // Values that are clearly placeholders or computed at runtime are not literals we can judge
            if (value.Length == 0)
            {
                return;
            }

            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https")
            {
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.InsecureEndpoint, argument.Syntax.GetLocation(), value, service));
            }
        }
    }
}
