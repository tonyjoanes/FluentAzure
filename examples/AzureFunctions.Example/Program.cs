using AzureFunctions.Example.Configuration;
using AzureFunctions.Example.Services;
using FluentAzure.Core;
using FluentAzure.Extensions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        // Configure FluentAzure with strongly-typed configuration
        services.AddSingleton<FunctionConfiguration>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<FunctionConfiguration>>();

            // Build configuration using FluentAzure
            var configResult = FluentAzure
                .Configuration()
                .FromEnvironment()
                .FromKeyVault(Environment.GetEnvironmentVariable("KeyVaultUrl"))
                .Required("DatabaseConnectionString")
                .Required("ServiceBusConnectionString")
                .Required("StorageConnectionString")
                .Optional("MaxRetryCount", "3")
                .Optional("TimeoutSeconds", "30")
                .Optional("EnableTelemetry", "true")
                .BuildAsync()
                .Result.Bind<FunctionConfiguration>();

            return configResult.Match(
                success =>
                {
                    logger.LogInformation("✅ Configuration loaded successfully");
                    logger.LogInformation("Database: {Database}", success.Database.Host);
                    logger.LogInformation(
                        "Service Bus: {ServiceBus}",
                        success.ServiceBus.Namespace
                    );
                    logger.LogInformation("Storage: {Storage}", success.Storage.AccountName);
                    return success;
                },
                errors =>
                {
                    logger.LogError(
                        "❌ Configuration failed to load: {Errors}",
                        string.Join(", ", errors)
                    );
                    throw new InvalidOperationException(
                        $"Configuration failed: {string.Join(", ", errors)}"
                    );
                }
            );
        });

        // Register services that depend on configuration
        services.AddSingleton<IDatabaseService, DatabaseService>();
        services.AddSingleton<IServiceBusService, ServiceBusService>();
        services.AddSingleton<IStorageService, StorageService>();
        services.AddSingleton<ITelemetryService, TelemetryService>();
    })
    .Build();

host.Run();
