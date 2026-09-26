// Native AOT example: the FluentAzure pipeline feeds IConfiguration, and options are bound with the
// configuration binding source generator rather than reflection.
//
//   dotnet publish -c Release -r linux-x64
//   ./bin/Release/net10.0/linux-x64/publish/Aot.Example
//
// Set KEYVAULT_URL and/or APPCONFIG_ENDPOINT to also load from Azure using a managed identity.
using FluentAzure;
using FluentAzure.Configuration;
using FluentAzure.Sources;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

var keyVaultUrl = Environment.GetEnvironmentVariable("KEYVAULT_URL");
var appConfigEndpoint = Environment.GetEnvironmentVariable("APPCONFIG_ENDPOINT");

IConfigurationRoot configuration = new ConfigurationBuilder()
    .AddFluentAzure(fluent =>
    {
        fluent
            .UseManagedIdentity()
            .AddSource(
                new InMemorySource(
                    new Dictionary<string, string>
                    {
                        ["App:Name"] = "AotDemo",
                        ["App:Port"] = "8080",
                        ["App:ApiKey"] = "not-a-real-key",
                    },
                    priority: 10
                )
            )
            .FromEnvironment()
            .Sensitive("App:ApiKey")
            .Required("App:Name")
            .Validate(c => int.TryParse(c["App:Port"], out _) ? null : "App:Port must be a number");

        if (!string.IsNullOrEmpty(keyVaultUrl))
        {
            fluent.FromKeyVault(keyVaultUrl);
        }

        if (!string.IsNullOrEmpty(appConfigEndpoint))
        {
            fluent.FromAppConfiguration(appConfigEndpoint, o => o.IncludeFeatureFlags = true);
        }

        return fluent;
    })
    .Build();

var services = new ServiceCollection();
services.AddOptions<AppOptions>().Bind(configuration.GetSection("App")).ValidateOnStart();
services.AddSingleton<IConfiguration>(configuration);
services.AddLogging();
services.AddHealthChecks().AddFluentAzure();

using var provider = services.BuildServiceProvider();
var options = provider.GetRequiredService<IOptions<AppOptions>>().Value;

var fluentProvider = configuration.Providers.OfType<FluentAzureConfigurationProvider>().Single();

// Never print raw configuration: FromEnvironment() loads every environment variable, and only keys
// FluentAzure knows are secret (Key Vault values, Sensitive(...) keys) are masked by GetRedactedDebugView()
Console.WriteLine($"Name={options.Name} Port={options.Port}");
Console.WriteLine($"App:ApiKey loaded={options.ApiKey.Length > 0} sensitive={fluentProvider.IsSensitive("App:ApiKey")}");

var health = await provider.GetRequiredService<HealthCheckService>().CheckHealthAsync();
Console.WriteLine($"Health={health.Status}");

return options.Name.Length > 0 && options.Port > 0 && health.Status == HealthStatus.Healthy ? 0 : 1;

internal sealed class AppOptions
{
    public string Name { get; set; } = string.Empty;

    public int Port { get; set; }

    public string ApiKey { get; set; } = string.Empty;
}
