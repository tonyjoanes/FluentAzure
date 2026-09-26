using Azure.Extensions.AspNetCore.Configuration.Secrets;
using FluentAssertions;
using FluentAzure.Configuration;
using FluentAzure.Guard;
using FluentAzure.Tests.TestDoubles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MsConfigurationBuilder = Microsoft.Extensions.Configuration.ConfigurationBuilder;

namespace FluentAzure.Tests.Guard;

/// <summary>
/// Tests for <see cref="ConfigurationGuard"/>: startup validation, health checks and redaction over any
/// configuration provider, including Microsoft's Key Vault provider.
/// </summary>
public class ConfigurationGuardTests
{
    private const string SecretValue = "hunter2-super-secret";

    [Fact]
    public void Check_WithMissingRequiredKeys_ShouldListEveryMissingKey()
    {
        // Arrange
        var guard = CreateGuard(g => g.Required("Database:ConnectionString", "Jwt:SecretKey", "Name"));
        var configuration = Config(new() { ["Name"] = "App" });

        // Act
        var failures = guard.Check(configuration);

        // Assert
        failures.Should().HaveCount(2);
        failures.Should().Contain(f => f.Contains("'Database:ConnectionString'"));
        failures.Should().Contain(f => f.Contains("'Jwt:SecretKey'"));
    }

    [Fact]
    public void Check_ShouldMatchKeysAcrossSeparatorsAndCase()
    {
        // Arrange
        var guard = CreateGuard(g => g.Required("database__host"));
        var configuration = Config(new() { ["Database:Host"] = "db.local" });

        // Act & Assert
        guard.Check(configuration).Should().BeEmpty();
    }

    [Fact]
    public void Check_WithRequiredSection_ShouldBeSatisfiedByChildren()
    {
        // Arrange
        var guard = CreateGuard(g => g.Required("ConnectionStrings"));
        var configuration = Config(new() { ["ConnectionStrings:Default"] = "Server=db" });

        // Act & Assert
        guard.Check(configuration).Should().BeEmpty();
    }

    [Fact]
    public void Check_WithEmptyValue_ShouldReportRequiredKey()
    {
        // Arrange
        var guard = CreateGuard(g => g.Required("Name"));
        var configuration = Config(new() { ["Name"] = string.Empty });

        // Act & Assert
        guard.Check(configuration).Should().ContainSingle().Which.Should().Contain("'Name'");
    }

    [Fact]
    public void Check_WithFailingRule_ShouldNameKeyAndRequirementButNotValue()
    {
        // Arrange
        var guard = CreateGuard(g => g.Validate("Jwt:SecretKey", v => v.Length >= 32, "must be at least 32 characters"));
        var configuration = Config(new() { ["Jwt:SecretKey"] = SecretValue });

        // Act
        var failures = guard.Check(configuration);

        // Assert
        failures.Should().ContainSingle().Which.Should()
            .Be("Configuration key 'Jwt:SecretKey' must be at least 32 characters.");
        string.Join(" ", failures).Should().NotContain(SecretValue);
    }

    [Fact]
    public void Check_WithRuleThatThrows_ShouldReportExceptionTypeButNotValue()
    {
        // Arrange: int.Parse echoes its input in the exception message on .NET 8+
        var guard = CreateGuard(g => g.Validate("Port", v => int.Parse(v, System.Globalization.CultureInfo.InvariantCulture) > 0, "must be a positive number"));
        var configuration = Config(new() { ["Port"] = SecretValue });

        // Act
        var failures = guard.Check(configuration);

        // Assert
        failures.Should().ContainSingle().Which.Should().Contain("'Port'").And.Contain(nameof(FormatException));
        string.Join(" ", failures).Should().NotContain(SecretValue);
    }

    [Fact]
    public void Check_WithRuleForMissingKey_ShouldSkipRule()
    {
        // Arrange
        var guard = CreateGuard(g => g.Validate("Optional:Url", v => v.StartsWith("https://", StringComparison.Ordinal), "must use https"));

        // Act & Assert
        guard.Check(Config(new())).Should().BeEmpty();
    }

    [Fact]
    public void ThrowIfInvalid_WithFailures_ShouldThrowWithAllErrors()
    {
        // Arrange
        var guard = CreateGuard(g => g.Required("A", "B"));

        // Act
        var act = () => guard.ThrowIfInvalid(Config(new()));

        // Assert
        act.Should().Throw<FluentAzureConfigurationException>().Which.Errors.Should().HaveCount(2);
    }

    [Fact]
    public async Task Host_WithMissingKeys_ShouldFailToStartListingEveryFailure()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:SecretKey"] = SecretValue });
        builder.Services.AddFluentAzureGuard(g => g
            .Required("Database:ConnectionString")
            .Validate("Jwt:SecretKey", v => v.Length >= 32, "must be at least 32 characters"));
        using var host = builder.Build();

        // Act
        var act = () => host.StartAsync();

        // Assert
        var exception = (await act.Should().ThrowAsync<OptionsValidationException>()).Which;
        exception.Failures.Should().HaveCount(2);
        exception.Message.Should().Contain("'Database:ConnectionString'").And.NotContain(SecretValue);
    }

    [Fact]
    public async Task Host_WithValidConfiguration_ShouldStart()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Database:ConnectionString"] = "Server=db" });
        builder.Services.AddFluentAzureGuard(g => g.Required("Database:ConnectionString"));
        using var host = builder.Build();

        // Act
        await host.StartAsync();

        // Assert
        host.Services.GetRequiredService<ConfigurationGuard>().Options.RequiredKeys.Should().ContainSingle();
        await host.StopAsync();
    }

    [Fact]
    public void Guard_WithMicrosoftKeyVaultProvider_ShouldValidateAndRedactItsSecrets()
    {
        // Arrange: Microsoft's provider maps the secret "Database--Password" to "Database:Password"
        var vault = new FakeVaultClient();
        vault.Add("Database--Password", SecretValue);
        var root = new MsConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Database:Host"] = "db.local" })
            .AddAzureKeyVault(vault, new AzureKeyVaultConfigurationOptions())
            .Build();
        var guard = CreateGuard(g => g.Required("Database:Host", "Database:Password"));

        // Act
        var failures = guard.Check(root);
        var view = root.GetRedactedDebugView();

        // Assert
        failures.Should().BeEmpty();
        view.Should().Contain("db.local").And.NotContain(SecretValue);
    }

    [Fact]
    public void GetRedactedDebugView_ShouldMaskCredentialShapedKeysAndValues()
    {
        // Arrange
        var root = Config(new()
        {
            ["ConnectionStrings:Default"] = "Server=db",
            ["Jwt:SecretKey"] = "jwt-" + SecretValue,
            ["Storage:Endpoint"] = "DefaultEndpointsProtocol=https;AccountName=a;AccountKey=" + SecretValue,
            ["Logging:Level"] = "Warning",
        });

        // Act
        var view = root.GetRedactedDebugView();

        // Assert
        view.Should().NotContain("Server=db").And.NotContain(SecretValue);
        view.Should().Contain("Warning");
    }

    [Fact]
    public void GuardRedactedDebugView_ShouldAlsoMaskDeclaredSensitiveKeys()
    {
        // Arrange
        var root = Config(new() { ["Payments:MerchantId"] = "merchant-42", ["App:Name"] = "Shop" });
        var guard = CreateGuard(g => g.Sensitive("payments:*"));

        // Act
        var plain = root.GetRedactedDebugView();
        var guarded = guard.GetRedactedDebugView(root, "[hidden]");

        // Assert
        plain.Should().Contain("merchant-42");
        guarded.Should().NotContain("merchant-42").And.Contain("[hidden]").And.Contain("Shop");
    }

    [Fact]
    public async Task HealthCheck_ShouldReportFailureCountWithoutValues()
    {
        // Arrange
        var values = new Dictionary<string, string?> { ["Jwt:SecretKey"] = SecretValue };
        var configuration = new MsConfigurationBuilder().AddInMemoryCollection(values).Build();
        var guard = CreateGuard(g => g
            .Required("Database:ConnectionString")
            .Validate("Jwt:SecretKey", v => v.Length >= 32, "must be at least 32 characters"));
        var check = new ConfigurationGuardHealthCheck(guard, configuration);

        // Act
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Data["failures"].Should().Be(2);
        result.Description.Should().NotContain(SecretValue);
    }

    [Fact]
    public async Task HealthCheck_RegisteredThroughDependencyInjection_ShouldBeHealthyWhenValid()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(Config(new() { ["Name"] = "App" }));
        services.AddFluentAzureGuard(g => g.Required("Name"));
        services.AddHealthChecks().AddFluentAzureGuard();
        using var provider = services.BuildServiceProvider();

        // Act
        var report = await provider.GetRequiredService<HealthCheckService>().CheckHealthAsync();

        // Assert
        report.Status.Should().Be(HealthStatus.Healthy);
        report.Entries.Should().ContainKey("fluentazure-guard");
    }

    private static ConfigurationGuard CreateGuard(Action<ConfigurationGuardOptions> configure)
    {
        var options = new ConfigurationGuardOptions();
        configure(options);
        return new ConfigurationGuard(options);
    }

    private static IConfigurationRoot Config(Dictionary<string, string?> values) =>
        new MsConfigurationBuilder().AddInMemoryCollection(values).Build();
}
