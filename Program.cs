using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using ContainerLogExporter;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

TokenCredential tokenCredential = new ManagedIdentityCredential();

await new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(builder => builder.AddApplicationInsights().AddApplicationInsightsLogger())
    .ConfigureServices(services => services
        .Configure<TelemetryConfiguration>(configuration => configuration.SetAzureTokenCredential(tokenCredential)).AddApplicationInsightsTelemetryWorkerService()
        .AddMemoryCache()
        .AddSingleton(_ => new ArmClient(tokenCredential))
        .AddSingleton<WorkspaceService>())
    .Build()
    .RunAsync();