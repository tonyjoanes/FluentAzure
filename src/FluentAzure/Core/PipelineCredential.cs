using Azure.Core;
using Azure.Identity;

namespace FluentAzure.Core;

/// <summary>
/// The credential shared by every Azure source in a pipeline. It resolves the configured identity
/// on first use, so <c>UseManagedIdentity()</c> and similar calls work in any order relative to
/// <c>FromKeyVault()</c>/<c>FromAppConfiguration()</c>, and all sources share one token cache.
/// </summary>
internal sealed class PipelineCredential : TokenCredential
{
    private readonly Lazy<TokenCredential> _inner;
    private TokenCredential? _configured;

    public PipelineCredential()
    {
        // DefaultAzureCredential honours AZURE_TOKEN_CREDENTIALS (e.g. "prod") to restrict its chain
        _inner = new Lazy<TokenCredential>(
            () => _configured ?? new DefaultAzureCredential(),
            LazyThreadSafetyMode.ExecutionAndPublication
        );
    }

    /// <summary>
    /// Gets a value indicating whether an explicit credential was configured.
    /// </summary>
    public bool IsConfigured => _configured is not null;

    /// <summary>
    /// Sets the credential to use. Must be called before the first token is requested.
    /// </summary>
    public void Configure(TokenCredential credential)
    {
        if (_inner.IsValueCreated)
        {
            throw new InvalidOperationException(
                "The pipeline credential cannot be changed after it has been used."
            );
        }

        _configured = credential;
    }

    /// <summary>
    /// Gets the credential that tokens are requested from.
    /// </summary>
    public TokenCredential Resolve() => _inner.Value;

    /// <summary>
    /// Creates a managed identity credential: system-assigned when no client ID is given, otherwise user-assigned.
    /// </summary>
    public static ManagedIdentityCredential CreateManagedIdentity(string? clientId) =>
        new(
            string.IsNullOrEmpty(clientId)
                ? ManagedIdentityId.SystemAssigned
                : ManagedIdentityId.FromUserAssignedClientId(clientId)
        );

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
        Resolve().GetToken(requestContext, cancellationToken);

    public override ValueTask<AccessToken> GetTokenAsync(
        TokenRequestContext requestContext,
        CancellationToken cancellationToken
    ) => Resolve().GetTokenAsync(requestContext, cancellationToken);
}
