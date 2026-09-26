using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using FluentAzure.Core;
using FluentAzure.Sources;

namespace FluentAzure.Tests.Binding;

/// <summary>
/// Tests that the typed pipeline APIs (<c>BuildAsync&lt;T&gt;()</c>, <c>Bind&lt;T&gt;()</c>) use the enhanced binder.
/// </summary>
public class TypedBuildBindingTests
{
    [Fact]
    public async Task BuildAsyncOfT_ShouldBindCollectionsAndDictionaries()
    {
        // Arrange
        var pipeline = FluentConfig.Create().AddSource(new InMemorySource(new()
        {
            ["Name"] = "App",
            ["Hosts:0"] = "a.local",
            ["Hosts:1"] = "b.local",
            ["Limits:Requests"] = "100",
        }));

        // Act
        var result = await pipeline.BuildAsync<Settings>();

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? string.Join("; ", result.Errors) : string.Empty);
        result.Value.Hosts.Should().Equal("a.local", "b.local");
        result.Value.Limits.Should().ContainKey("Requests").WhoseValue.Should().Be(100);
    }

    [Fact]
    public async Task BuildAsyncOfT_ShouldRunDataAnnotationsValidation()
    {
        // Arrange
        var pipeline = FluentConfig.Create().AddSource(new InMemorySource(new() { ["Name"] = "App", ["Port"] = "70000" }));

        // Act
        var result = await pipeline.BuildAsync<Settings>();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().Contain("Port");
    }

    [Fact]
    public async Task ResultBind_ShouldUseTheSameBinder()
    {
        // Arrange
        var build = await FluentConfig.Create().AddSource(new InMemorySource(new() { ["Hosts:0"] = "a.local" })).BuildAsync();

        // Act
        var result = build.Bind<Settings>();

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? string.Join("; ", result.Errors) : string.Empty);
        result.Value.Hosts.Should().Equal("a.local");
    }

    public sealed class Settings
    {
        public string Name { get; set; } = string.Empty;

        [Range(1, 65535)]
        public int Port { get; set; } = 443;

        public List<string> Hosts { get; set; } = new();

        public Dictionary<string, int> Limits { get; set; } = new();
    }
}
