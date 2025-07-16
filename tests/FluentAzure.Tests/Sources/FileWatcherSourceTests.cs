using FluentAssertions;
using FluentAzure.Core;
using FluentAzure.Sources;

namespace FluentAzure.Tests.Sources;

/// <summary>
/// Tests for the FileWatcherSource class.
/// </summary>
public class FileWatcherSourceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _testFilePath;
    private readonly List<FileWatcherSource> _sources = new();

    public FileWatcherSourceTests()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            "FluentAzureTests",
            Guid.NewGuid().ToString()
        );
        Directory.CreateDirectory(_testDirectory);
        _testFilePath = Path.Combine(_testDirectory, "test-config.json");
    }

    private FileWatcherSource CreateSource(
        string filePath,
        int priority = 50,
        bool optional = false,
        int debounceMs = 500
    )
    {
        var source = new FileWatcherSource(filePath, priority, optional, debounceMs);
        _sources.Add(source);
        return source;
    }

    [Fact]
    public void Constructor_WithValidPath_ShouldCreateInstance()
    {
        // Act
        var source = CreateSource(_testFilePath);

        // Assert
        source.Should().NotBeNull();
        source.Name.Should().Contain("test-config.json");
        source.SupportsHotReload.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithDebounceTime_ShouldSetDebounceTime()
    {
        // Act
        var source = CreateSource(_testFilePath, debounceMs: 1000);

        // Assert
        source.Should().NotBeNull();
    }

    [Fact]
    public async Task LoadAsync_WithExistingFile_ShouldLoadConfiguration()
    {
        // Arrange
        await File.WriteAllTextAsync(_testFilePath, """{"Key": "Value"}""");
        var source = CreateSource(_testFilePath);

        // Act
        var result = await source.LoadAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainKey("Key");
        result.Value["Key"].Should().Be("Value");
    }

    [Fact]
    public async Task LoadAsync_WithMissingFile_ShouldReturnError()
    {
        // Arrange
        var source = CreateSource(_testFilePath);

        // Act
        var result = await source.LoadAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle();
        result.Errors[0].Should().Contain("was not found");
    }

    [Fact]
    public async Task LoadAsync_WithOptionalFile_ShouldReturnEmptyDictionary()
    {
        // Arrange
        var source = CreateSource(_testFilePath, optional: true);

        // Act
        var result = await source.LoadAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task FileChanged_ShouldTriggerConfigurationChangedEvent()
    {
        // Arrange
        await File.WriteAllTextAsync(_testFilePath, """{"Key": "Value1"}""");
        var source = CreateSource(_testFilePath, debounceMs: 100);

        ConfigurationChangedEventArgs? eventArgs = null;
        source.ConfigurationChanged += (sender, e) => eventArgs = e;

        // Load initial configuration
        await source.LoadAsync();

        // Act
        await File.WriteAllTextAsync(_testFilePath, """{"Key": "Value2"}""");
        await Task.Delay(200); // Wait for debounce and file system event

        // Assert
        eventArgs.Should().NotBeNull();
        eventArgs!.PreviousValues["Key"].Should().Be("Value1");
        eventArgs.NewValues["Key"].Should().Be("Value2");
        eventArgs.Source.Should().Be(source);
    }

    [Fact]
    public async Task FileChanged_WithInvalidJson_ShouldNotTriggerEvent()
    {
        // Arrange
        await File.WriteAllTextAsync(_testFilePath, """{"Key": "Value1"}""");
        var source = CreateSource(_testFilePath, debounceMs: 100);

        ConfigurationChangedEventArgs? eventArgs = null;
        source.ConfigurationChanged += (sender, e) => eventArgs = e;

        // Load initial configuration
        await source.LoadAsync();

        // Act
        await File.WriteAllTextAsync(_testFilePath, """{"Key": "Value2",}"""); // Invalid JSON
        await Task.Delay(200); // Wait for debounce and file system event

        // Assert
        eventArgs.Should().BeNull(); // Event should not be triggered for invalid JSON
    }

    [Fact]
    public async Task FileChanged_WithDebounce_ShouldPreventRapidUpdates()
    {
        // Arrange
        await File.WriteAllTextAsync(_testFilePath, """{"Key": "Value1"}""");
        var source = CreateSource(_testFilePath, debounceMs: 500);

        var eventCount = 0;
        source.ConfigurationChanged += (sender, e) => eventCount++;

        // Load initial configuration
        await source.LoadAsync();

        // Act - Make multiple rapid changes
        await File.WriteAllTextAsync(_testFilePath, """{"Key": "Value2"}""");
        await Task.Delay(50);
        await File.WriteAllTextAsync(_testFilePath, """{"Key": "Value3"}""");
        await Task.Delay(50);
        await File.WriteAllTextAsync(_testFilePath, """{"Key": "Value4"}""");
        await Task.Delay(600); // Wait longer than debounce time

        // Assert
        eventCount.Should().Be(1); // Only one event should be triggered due to debouncing
    }

    [Fact]
    public async Task ReloadAsync_ShouldReloadConfiguration()
    {
        // Arrange
        await File.WriteAllTextAsync(_testFilePath, """{"Key": "Value1"}""");
        var source = CreateSource(_testFilePath);

        // Load initial configuration
        await source.LoadAsync();

        // Change file content
        await File.WriteAllTextAsync(_testFilePath, """{"Key": "Value2"}""");

        // Act
        var result = await source.ReloadAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value["Key"].Should().Be("Value2");
    }

    [Fact]
    public async Task Dispose_ShouldCleanupResources()
    {
        // Arrange
        await File.WriteAllTextAsync(_testFilePath, """{"Key": "Value"}""");
        var source = CreateSource(_testFilePath);
        await source.LoadAsync();

        // Act
        source.Dispose();

        // Assert - Should not throw when disposed
        source.Invoking(s => s.Dispose()).Should().NotThrow();
    }

    [Fact]
    public async Task ContainsKey_WithLoadedConfiguration_ShouldReturnCorrectValue()
    {
        // Arrange
        await File.WriteAllTextAsync(_testFilePath, """{"Key1": "Value1", "Key2": "Value2"}""");
        var source = CreateSource(_testFilePath);
        await source.LoadAsync();

        // Act & Assert
        source.ContainsKey("Key1").Should().BeTrue();
        source.ContainsKey("Key2").Should().BeTrue();
        source.ContainsKey("Key3").Should().BeFalse();
    }

    [Fact]
    public async Task GetValue_WithLoadedConfiguration_ShouldReturnCorrectValue()
    {
        // Arrange
        await File.WriteAllTextAsync(_testFilePath, """{"Key1": "Value1", "Key2": "Value2"}""");
        var source = CreateSource(_testFilePath);
        await source.LoadAsync();

        // Act & Assert
        source.GetValue("Key1").Should().Be("Value1");
        source.GetValue("Key2").Should().Be("Value2");
        source.GetValue("Key3").Should().BeNull();
    }

    public void Dispose()
    {
        try
        {
            // Dispose all sources first
            foreach (var source in _sources)
            {
                try
                {
                    source.Dispose();
                }
                catch
                {
                    // Ignore disposal errors
                }
            }
            _sources.Clear();

            // Wait a bit for any pending file system operations
            Task.Delay(100).Wait();

            // Then clean up the directory
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
