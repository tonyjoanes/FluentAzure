using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace FluentAzure;

/// <summary>
/// OpenTelemetry-compatible tracing and metrics for FluentAzure. Enable them with
/// <c>.AddSource(FluentAzureDiagnostics.ActivitySourceName)</c> and
/// <c>.AddMeter(FluentAzureDiagnostics.MeterName)</c>. Telemetry carries source names, outcomes,
/// durations and counts only — never configuration keys' values or error messages.
/// </summary>
public static class FluentAzureDiagnostics
{
    /// <summary>
    /// The name of the <see cref="System.Diagnostics.ActivitySource"/> used for traces.
    /// </summary>
    public const string ActivitySourceName = "FluentAzure";

    /// <summary>
    /// The name of the <see cref="System.Diagnostics.Metrics.Meter"/> used for metrics.
    /// </summary>
    public const string MeterName = "FluentAzure";

    internal const string SourceTag = "fluentazure.source";
    internal const string OutcomeTag = "fluentazure.outcome";
    internal const string ReloadTag = "fluentazure.reload";

    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName, Version.Full);

    internal static readonly Meter Meter = new(MeterName, Version.Full);

    /// <summary>
    /// Time taken by a single configuration source to load, tagged by source and outcome.
    /// </summary>
    internal static readonly Histogram<double> SourceLoadDuration = Meter.CreateHistogram<double>(
        "fluentazure.source.load.duration",
        unit: "s",
        description: "Duration of loading a configuration source."
    );

    /// <summary>
    /// Time taken by a whole pipeline build: sources, required keys, transforms and validations.
    /// </summary>
    internal static readonly Histogram<double> BuildDuration = Meter.CreateHistogram<double>(
        "fluentazure.build.duration",
        unit: "s",
        description: "Duration of building a FluentAzure configuration pipeline."
    );

    /// <summary>
    /// Reloads performed by the IConfiguration provider, tagged changed/unchanged/failed.
    /// </summary>
    internal static readonly Counter<long> ProviderReloads = Meter.CreateCounter<long>(
        "fluentazure.provider.reloads",
        unit: "{reload}",
        description: "Reloads performed by the FluentAzure configuration provider."
    );

    internal static class Outcomes
    {
        public const string Success = "success";
        public const string Failure = "failure";
        public const string Changed = "changed";
        public const string Unchanged = "unchanged";
    }
}
