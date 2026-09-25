using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using FluentAssertions;
using FluentAzure.Configuration;
using FluentAzure.Tests.TestDoubles;
using FluentPipeline = FluentAzure.Core.ConfigurationBuilder;
using MsConfigurationBuilder = Microsoft.Extensions.Configuration.ConfigurationBuilder;

namespace FluentAzure.Tests;

/// <summary>
/// Tests for FluentAzure's OpenTelemetry-compatible traces and metrics. Tests run in parallel, so each one
/// uses a uniquely named source and filters what it observes by that name.
/// </summary>
public class DiagnosticsTests
{
    private const string SecretValue = "super-secret-value";

    [Fact]
    public async Task BuildAsync_EmitsBuildSpanWithChildSpanPerSource()
    {
        // Arrange
        var sourceName = UniqueName();
        using var capture = new ActivityCapture();
        var builder = new FluentPipeline().AddSource(
            new ControllableSource(sourceName, new() { ["A"] = "1", ["B"] = "2" })
        );

        // Act
        var result = await builder.BuildAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        var load = capture.Single("FluentAzure.LoadSource", sourceName);
        load.GetTagItem("fluentazure.outcome").Should().Be("success");
        load.GetTagItem("fluentazure.key.count").Should().Be(2);
        load.GetTagItem("fluentazure.reload").Should().Be(false);

        var build = capture.Activities.Single(a => a.OperationName == "FluentAzure.Build" && a.SpanId == load.ParentSpanId);
        build.GetTagItem("fluentazure.outcome").Should().Be("success");
        build.Status.Should().Be(ActivityStatusCode.Unset);
    }

    [Fact]
    public async Task BuildAsync_WhenPipelineFails_MarksSpansAsErrorsWithoutMessagesOrValues()
    {
        // Arrange
        var sourceName = UniqueName();
        using var capture = new ActivityCapture();
        var builder = new FluentPipeline()
            .AddSource(new ControllableSource(sourceName) { FailWith = $"could not read {SecretValue}" });

        // Act
        var result = await builder.BuildAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        var load = capture.Single("FluentAzure.LoadSource", sourceName);
        load.Status.Should().Be(ActivityStatusCode.Error);
        load.GetTagItem("fluentazure.outcome").Should().Be("failure");

        var build = capture.Activities.Single(a => a.OperationName == "FluentAzure.Build" && a.SpanId == load.ParentSpanId);
        build.Status.Should().Be(ActivityStatusCode.Error);
        build.GetTagItem("fluentazure.error.count").Should().Be(1);

        foreach (var activity in new[] { load, build })
        {
            activity.StatusDescription.Should().NotContain(SecretValue);
            activity.TagObjects.Select(t => t.Value?.ToString()).Should().NotContain(v => v != null && v.Contains(SecretValue));
        }
    }

    [Fact]
    public async Task BuildAsync_WhenCustomSourceThrows_ReturnsErrorNamingSourceWithoutExceptionMessage()
    {
        // Arrange
        var sourceName = UniqueName();
        var builder = new FluentPipeline()
            .AddSource(new ControllableSource(sourceName) { ThrowWith = new InvalidOperationException(SecretValue) });

        // Act
        var result = await builder.BuildAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().Contain(sourceName).And.Contain(nameof(InvalidOperationException)).And.NotContain(SecretValue);
    }

    [Fact]
    public async Task BuildAsync_RecordsSourceAndBuildDurations()
    {
        // Arrange
        var sourceName = UniqueName();
        using var capture = new MetricCapture();
        var builder = new FluentPipeline().AddSource(new ControllableSource(sourceName, new() { ["A"] = "1" }));

        // Act
        await builder.BuildAsync();

        // Assert
        capture.Measurements.Should().Contain(m =>
            m.Instrument == "fluentazure.source.load.duration"
            && Equals(m.Tags["fluentazure.source"], sourceName)
            && Equals(m.Tags["fluentazure.outcome"], "success")
            && m.Value >= 0);
        capture.Measurements.Should().Contain(m => m.Instrument == "fluentazure.build.duration");
    }

    [Fact]
    public async Task ProviderReloads_AreCountedByOutcome()
    {
        // Arrange
        var source = new ControllableSource(UniqueName(), new() { ["Key"] = "v1" });
        using var capture = new MetricCapture();
        var configuration = new MsConfigurationBuilder().AddFluentAzure(fluent => fluent.AddSource(source)).Build();
        var provider = configuration.Providers.OfType<FluentAzureConfigurationProvider>().Single();

        // Act
        await provider.ReloadAsync();
        source.Values["Key"] = "v2";
        await provider.ReloadAsync();
        source.FailWith = "unavailable";
        await provider.ReloadAsync();

        // Assert
        var outcomes = capture.Measurements
            .Where(m => m.Instrument == "fluentazure.provider.reloads")
            .Select(m => m.Tags["fluentazure.outcome"])
            .ToList();
        outcomes.Should().Contain(new object[] { "unchanged", "changed", "failure" });
    }

    [Fact]
    public void DiagnosticNames_AreStable()
    {
        FluentAzureDiagnostics.ActivitySourceName.Should().Be("FluentAzure");
        FluentAzureDiagnostics.MeterName.Should().Be("FluentAzure");
    }

    private static string UniqueName() => $"Test-{Guid.NewGuid():N}";

    private sealed class ActivityCapture : IDisposable
    {
        private readonly ActivityListener _listener;

        public ActivityCapture()
        {
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == FluentAzureDiagnostics.ActivitySourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity => Activities.Add(activity),
            };
            ActivitySource.AddActivityListener(_listener);
        }

        public ConcurrentBag<Activity> Activities { get; } = new();

        public Activity Single(string operationName, string sourceName) =>
            Activities.Single(a => a.OperationName == operationName && Equals(a.GetTagItem("fluentazure.source"), sourceName));

        public void Dispose() => _listener.Dispose();
    }

    private sealed class MetricCapture : IDisposable
    {
        private readonly MeterListener _listener = new();

        public MetricCapture()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == FluentAzureDiagnostics.MeterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Record(instrument, value, tags));
            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Record(instrument, value, tags));
            _listener.Start();
        }

        public ConcurrentBag<(string Instrument, double Value, Dictionary<string, object?> Tags)> Measurements { get; } = new();

        public void Dispose() => _listener.Dispose();

        private void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var tagMap = new Dictionary<string, object?>();
            foreach (var tag in tags)
            {
                tagMap[tag.Key] = tag.Value;
            }

            Measurements.Add((instrument.Name, value, tagMap));
        }
    }
}
