using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FluentAzure.Guard;

/// <summary>
/// Re-checks a <see cref="ConfigurationGuard"/>'s rules against the current configuration, so a reload that
/// removes a required key or brings in an invalid value is reported. It reads configuration only and does not
/// call Azure. Reported data contains the number of failures, never keys' values.
/// </summary>
public sealed class ConfigurationGuardHealthCheck : IHealthCheck
{
    private readonly ConfigurationGuard _guard;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationGuardHealthCheck"/> class.
    /// </summary>
    /// <param name="guard">The guard whose rules to check.</param>
    /// <param name="configuration">The application configuration.</param>
    public ConfigurationGuardHealthCheck(ConfigurationGuard guard, IConfiguration configuration)
    {
        _guard = guard ?? throw new ArgumentNullException(nameof(guard));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(context);

        var failures = _guard.Check(_configuration);
        if (failures.Count == 0)
        {
            return Task.FromResult(HealthCheckResult.Healthy("All configuration requirements are met."));
        }

        var data = new Dictionary<string, object> { ["failures"] = failures.Count };
        return Task.FromResult(
            new HealthCheckResult(
                context.Registration?.FailureStatus ?? HealthStatus.Unhealthy,
                $"{failures.Count} configuration requirement(s) failed.",
                data: data
            )
        );
    }
}
