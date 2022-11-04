using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Blobs;
using ContainerLogExporter;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

TokenCredential tokenCredential = new ManagedIdentityCredential();

await new HostBuilder()
    .ConfigureAppConfiguration(builder =>
        builder.AddAzureAppConfiguration(options =>
            options.Connect(new Uri(Environment.GetEnvironmentVariable("APP_CONFIGURATION_URI")!), tokenCredential).Select(KeyFilter.Any, "namespace")))
    .ConfigureServices(services => services
        .Configure<TelemetryConfiguration>(configuration => configuration.SetAzureTokenCredential(tokenCredential)).AddApplicationInsightsTelemetryWorkerService()
        .AddAzureAppConfiguration()
        .AddSingleton(provider => new BlobContainerClient(provider.GetRequiredService<IConfiguration>().GetValue<Uri>("BLOB_CONTAINER_URI"), tokenCredential))
        .AddSingleton(provider => new SecretClient(provider.GetRequiredService<IConfiguration>().GetValue<Uri>("VAULT_URI"), tokenCredential))
        .AddSingleton<WorkspaceService>())
    .ConfigureFunctionsWorkerDefaults(app =>
    {
        app.AddApplicationInsights().AddApplicationInsightsLogger();
        app.UseAzureAppConfiguration();
    })
    .Build()
    .RunAsync();