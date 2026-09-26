using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FluentAzure.Configuration;

/// <summary>
/// Reports the state of the FluentAzure configuration providers in an <see cref="IConfiguration"/>.
/// It does not call Azure: it reports what the providers last observed, so it is cheap enough for
/// frequent liveness/readiness probes.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Healthy: every provider's last load or reload succeeded and its values are fresh.</item>
/// <item>Degraded: a reload failed; the provider keeps serving the last good values.</item>
/// <item>Failure status (Unhealthy by default): no FluentAzure provider is registered, or values are
/// older than the staleness threshold (by default three reload intervals, when periodic reload is on).</item>
/// </list>
/// Reported data contains counts and timestamps only — never keys' values or error messages,
/// since health endpoints are often exposed publicly.
/// </remarks>
public sealed class FluentAzureHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly TimeSpan? _staleAfter;

    /// <summary>
    /// Initializes a new instance of the <see cref="FluentAzureHealthCheck"/> class.
    /// </summary>
    /// <param name="configuration">The application configuration containing FluentAzure providers.</param>
    /// <param name="staleAfter">
    /// How old the last successful load may be before the check fails. Defaults to three times the
    /// provider's reload interval; providers without periodic reload are never considered stale.
    /// </param>
    public FluentAzureHealthCheck(IConfiguration configuration, TimeSpan? staleAfter = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _staleAfter = staleAfter;
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        var failureStatus = context.Registration?.FailureStatus ?? HealthStatus.Unhealthy;

        var providers = (_configuration as IConfigurationRoot)
            ?.Providers.OfType<FluentAzureConfigurationProvider>()
            .ToList();

        if (providers is null || providers.Count == 0)
        {
            return Task.FromResult(
                new HealthCheckResult(failureStatus, "No FluentAzure configuration provider is registered.")
            );
        }

        var data = new Dictionary<string, object>();
        var status = HealthStatus.Healthy;
        var problems = new List<string>();

        for (var i = 0; i < providers.Count; i++)
        {
            var provider = providers[i];
            var prefix = providers.Count == 1 ? string.Empty : $"provider{i}.";
            var now = provider.TimeProvider.GetUtcNow();

            data[$"{prefix}keys"] = provider.KeyCount;
            if (provider.LastSuccessfulLoad is { } lastSuccess)
            {
                data[$"{prefix}lastSuccessfulLoad"] = lastSuccess.ToString("O", CultureInfo.InvariantCulture);
            }

            if (provider.LastReloadErrors.Count > 0)
            {
                data[$"{prefix}lastReloadErrorCount"] = provider.LastReloadErrors.Count;
                problems.Add($"the last reload failed with {provider.LastReloadErrors.Count} error(s); serving last good values");
                status = Worst(status, HealthStatus.Degraded);
            }

            var staleAfter = _staleAfter ?? provider.ReloadInterval * 3;
            if (staleAfter is { } threshold && provider.LastSuccessfulLoad is { } last && now - last > threshold)
            {
                data[$"{prefix}staleFor"] = (now - last).ToString("c", CultureInfo.InvariantCulture);
                problems.Add($"values have not been refreshed for {(now - last).TotalSeconds:F0}s");
                status = Worst(status, failureStatus);
            }
        }

        var description = problems.Count == 0
            ? $"{providers.Count} FluentAzure provider(s) loaded."
            : $"FluentAzure configuration: {string.Join("; ", problems)}.";

        return Task.FromResult(new HealthCheckResult(status, description, data: data));
    }

    private static HealthStatus Worst(HealthStatus current, HealthStatus candidate) =>
        candidate < current ? candidate : current;
}
