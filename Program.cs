using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using ContainerLogExporter;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

TokenCredential tokenCredential = new ManagedIdentityCredential();

await new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services => services
        .Configure<TelemetryConfiguration>(configuration => configuration.SetAzureTokenCredential(tokenCredential)).AddApplicationInsightsTelemetryWorkerService()
        .AddSingleton<SecretService>()
        .AddSingleton<WorkspaceService>()
        .AddSingleton(provider => new SecretClient(provider.GetRequiredService<IConfiguration>().GetValue<Uri>("VAULT_URI"), tokenCredential)))
    .Build()
    .RunAsync();