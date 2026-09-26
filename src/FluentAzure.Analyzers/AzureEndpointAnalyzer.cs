using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace FluentAzure.Analyzers
{
    /// <summary>
    /// FAZ0002: Key Vault / App Configuration endpoints given as literals must be absolute HTTPS URIs.
    /// FAZ0003: App Configuration connection strings containing an access key secret must not be hard-coded.
    /// Covers FluentAzure's own APIs, Microsoft's configuration providers (<c>AddAzureAppConfiguration</c>,
    /// <c>Connect</c>, <c>AddAzureKeyVault</c>) and the Azure SDK clients (<c>SecretClient</c>,
    /// <c>ConfigurationClient</c>).
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class AzureEndpointAnalyzer : DiagnosticAnalyzer
    {
        private const string KeyVault = "Key Vault";
        private const string AppConfiguration = "App Configuration";

        // Microsoft and Azure SDK types whose "connectionString", "endpoint" and "vaultUri" parameters are checked
        private static readonly Dictionary<string, string> MicrosoftTypes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Microsoft.Extensions.Configuration.AzureAppConfigurationExtensions"] = AppConfiguration,
            ["Microsoft.Extensions.Configuration.AzureAppConfiguration.AzureAppConfigurationOptions"] = AppConfiguration,
            ["Azure.Data.AppConfiguration.ConfigurationClient"] = AppConfiguration,
            ["Microsoft.Extensions.Configuration.AzureKeyVaultConfigurationExtensions"] = KeyVault,
            ["Azure.Security.KeyVault.Secrets.SecretClient"] = KeyVault,
        };

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
            if (MicrosoftTypes.TryGetValue(method.ContainingType.ToDisplayString(), out var service))
            {
                CheckMicrosoftArguments(context, invocation.Arguments, service);
                return;
            }

            if (!KnownSymbols.IsPipelineMethod(method))
            {
                return;
            }

            if (method.Name.StartsWith("FromKeyVault", StringComparison.Ordinal)
                && KnownSymbols.TryGetStringArgument(invocation, "vaultUrl", out var vaultUrl, out var vaultArgument))
            {
                CheckEndpoint(context, vaultUrl, vaultArgument!, KeyVault);
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
            if (typeName != null && MicrosoftTypes.TryGetValue(typeName, out var service))
            {
                CheckMicrosoftArguments(context, creation.Arguments, service);
            }
            else if (typeName == "FluentAzure.Sources.KeyVaultSource"
                && KnownSymbols.TryGetStringArgument(creation, 0, out var vaultUrl, out var vaultArgument))
            {
                CheckEndpoint(context, vaultUrl, vaultArgument!, KeyVault);
            }
            else if (typeName == "FluentAzure.Sources.AppConfigurationSource"
                && KnownSymbols.TryGetStringArgument(creation, 0, out var connectionString, out var connectionArgument))
            {
                CheckAppConfiguration(context, connectionString, connectionArgument!);
            }
        }

        private static void CheckMicrosoftArguments(
            OperationAnalysisContext context,
            ImmutableArray<IArgumentOperation> arguments,
            string service)
        {
            foreach (var argument in arguments)
            {
                switch (argument.Parameter?.Name)
                {
                    case "connectionString"
                        when argument.Value.ConstantValue.HasValue && argument.Value.ConstantValue.Value is string connectionString:
                        CheckAppConfiguration(context, connectionString, argument);
                        break;
                    case "endpoint":
                    case "vaultUri":
                        if (TryGetUriLiteral(argument.Value, out var uri, out var literal))
                        {
                            CheckEndpoint(context, uri, literal!, service);
                        }

                        break;
                }
            }
        }

        // Finds "new Uri("...")" (possibly behind conversions) and returns its string literal
        private static bool TryGetUriLiteral(IOperation value, out string uri, out IArgumentOperation? literal)
        {
            while (value is IConversionOperation conversion)
            {
                value = conversion.Operand;
            }

            if (value is IObjectCreationOperation creation
                && creation.Type?.ToDisplayString() == "System.Uri"
                && KnownSymbols.TryGetStringArgument(creation, 0, out uri, out literal))
            {
                return true;
            }

            uri = string.Empty;
            literal = null;
            return false;
        }

        private static void CheckAppConfiguration(OperationAnalysisContext context, string value, IArgumentOperation argument)
        {
            if (value.IndexOf("Secret=", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.HardcodedConnectionStringSecret, argument.Syntax.GetLocation()));
            }
            else if (value.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                CheckEndpoint(context, value, argument, AppConfiguration);
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
