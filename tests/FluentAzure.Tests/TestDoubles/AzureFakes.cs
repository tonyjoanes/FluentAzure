using System.Diagnostics.CodeAnalysis;
using Azure;
using Azure.Core;
using Azure.Security.KeyVault.Secrets;

namespace FluentAzure.Tests.TestDoubles;

/// <summary>
/// Minimal HTTP response for building Azure SDK <see cref="Response{T}"/> and <see cref="Page{T}"/> values in tests.
/// </summary>
internal sealed class FakeResponse : Response
{
    public FakeResponse(int status) => Status = status;

    public override int Status { get; }

    public override string ReasonPhrase => string.Empty;

    public override Stream? ContentStream { get; set; }

    public override string ClientRequestId { get; set; } = string.Empty;

    public override void Dispose()
    {
    }

    protected override bool ContainsHeader(string name) => false;

    protected override IEnumerable<HttpHeader> EnumerateHeaders() => Array.Empty<HttpHeader>();

    protected override bool TryGetHeader(string name, [NotNullWhen(true)] out string? value)
    {
        value = null;
        return false;
    }

    protected override bool TryGetHeaderValues(string name, [NotNullWhen(true)] out IEnumerable<string>? values)
    {
        values = null;
        return false;
    }
}

/// <summary>
/// In-memory Key Vault supporting secret listing and retrieval, with optional per-read delay and failures.
/// Tracks call counts and the peak number of concurrent reads.
/// </summary>
internal sealed class FakeVaultClient : SecretClient
{
    private readonly object _gate = new();
    private int _inFlight;

    public FakeVaultClient(string vaultUri = "https://fake-vault.vault.azure.net/") =>
        VaultUri = new Uri(vaultUri);

    public override Uri VaultUri { get; }

    public Dictionary<string, (string Value, SecretProperties Properties)> Secrets { get; } = new();

    public HashSet<string> FailingSecrets { get; } = new();

    public TimeSpan ReadDelay { get; set; } = TimeSpan.Zero;

    public int ListCalls { get; private set; }

    public int GetCalls { get; private set; }

    public int PeakConcurrentReads { get; private set; }

    public void Add(
        string name,
        string value,
        bool? enabled = true,
        DateTimeOffset? expiresOn = null,
        DateTimeOffset? notBefore = null
    ) =>
        Secrets[name] = (
            value,
            new SecretProperties(name)
            {
                Enabled = enabled,
                ExpiresOn = expiresOn,
                NotBefore = notBefore,
            }
        );

    public override AsyncPageable<SecretProperties> GetPropertiesOfSecretsAsync(
        CancellationToken cancellationToken = default
    )
    {
        lock (_gate)
        {
            ListCalls++;
        }

        var page = Page<SecretProperties>.FromValues(
            Secrets.Values.Select(s => s.Properties).ToList(),
            null,
            new FakeResponse(200)
        );
        return AsyncPageable<SecretProperties>.FromPages(new[] { page });
    }

    // Azure.Security.KeyVault.Secrets 4.11 has two GetSecretAsync overloads; callers may bind to either
    public override Task<Response<KeyVaultSecret>> GetSecretAsync(
        string name,
        string? version = null,
        CancellationToken cancellationToken = default
    ) => GetSecretAsync(name, version, null, cancellationToken);

    public override async Task<Response<KeyVaultSecret>> GetSecretAsync(
        string name,
        string? version,
        SecretContentType? outContentType,
        CancellationToken cancellationToken = default
    )
    {
        lock (_gate)
        {
            GetCalls++;
            _inFlight++;
            PeakConcurrentReads = Math.Max(PeakConcurrentReads, _inFlight);
        }

        try
        {
            if (ReadDelay > TimeSpan.Zero)
            {
                await Task.Delay(ReadDelay, cancellationToken);
            }

            if (FailingSecrets.Contains(name) || !Secrets.TryGetValue(name, out var secret))
            {
                throw new RequestFailedException(404, $"Secret '{name}' not found");
            }

            return Response.FromValue(
                SecretModelFactory.KeyVaultSecret(secret.Properties, secret.Value),
                new FakeResponse(200)
            );
        }
        finally
        {
            lock (_gate)
            {
                _inFlight--;
            }
        }
    }
}
