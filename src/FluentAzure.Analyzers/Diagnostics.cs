using Microsoft.CodeAnalysis;

namespace FluentAzure.Analyzers
{
    /// <summary>
    /// Diagnostic descriptors for the FluentAzure analyzers.
    /// </summary>
    internal static class Diagnostics
    {
        private const string HelpLinkBase = "https://github.com/tonyjoanes/FluentAzure/blob/main/docs/analyzers.md#";

        /// <summary>
        /// FAZ0001: a <c>Required</c> key that the <c>BuildAsync&lt;T&gt;()</c> type never binds.
        /// </summary>
        public static readonly DiagnosticDescriptor RequiredKeyNotBound = new DiagnosticDescriptor(
            id: "FAZ0001",
            title: "Required key is not bound by the target type",
            messageFormat: "Required key '{0}' does not match any property of '{1}'; the build will require it but the value will never be bound",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "A key passed to Required() in a pipeline that ends in BuildAsync<T>() does not correspond to any property path of T. This is usually a typo in the key or property name.",
            helpLinkUri: HelpLinkBase + "faz0001");

        /// <summary>
        /// FAZ0002: a Key Vault or App Configuration endpoint that is not an absolute HTTPS URI.
        /// </summary>
        public static readonly DiagnosticDescriptor InsecureEndpoint = new DiagnosticDescriptor(
            id: "FAZ0002",
            title: "Azure endpoint is not an absolute HTTPS URI",
            messageFormat: "'{0}' is not an absolute https:// URI; {1} endpoints must use HTTPS",
            category: "Security",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Key Vault and App Configuration endpoints must be absolute HTTPS URIs. Plain HTTP would send credentials and secrets unencrypted.",
            helpLinkUri: HelpLinkBase + "faz0002");

        /// <summary>
        /// FAZ0003: a hard-coded App Configuration connection string that contains an access key secret.
        /// </summary>
        public static readonly DiagnosticDescriptor HardcodedConnectionStringSecret = new DiagnosticDescriptor(
            id: "FAZ0003",
            title: "App Configuration connection string with a secret is hard-coded",
            messageFormat: "This App Configuration connection string contains an access key secret; load it from configuration, or use an endpoint with a managed identity",
            category: "Security",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Connection strings containing 'Secret=' grant access to the store and end up in source control. Prefer an https endpoint with Microsoft Entra ID (UseManagedIdentity()).",
            helpLinkUri: HelpLinkBase + "faz0003");

        /// <summary>
        /// FAZ0004: a <c>GetDebugView()</c> call on configuration that FluentAzure contributes to.
        /// </summary>
        public static readonly DiagnosticDescriptor UnredactedDebugView = new DiagnosticDescriptor(
            id: "FAZ0004",
            title: "GetDebugView() prints secret values",
            messageFormat: "GetDebugView() prints every configuration value, including Key Vault secrets; use GetRedactedDebugView() instead",
            category: "Security",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "IConfigurationRoot.GetDebugView() includes secret values loaded by FluentAzure. GetRedactedDebugView() masks values FluentAzure knows are secrets.",
            helpLinkUri: HelpLinkBase + "faz0004");
    }
}
