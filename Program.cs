using System.Net.Mime;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.Storage.Blobs;
using ContainerLogExporter;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Polly;

TokenCredential tokenCredential = new ManagedIdentityCredential();

await new HostBuilder()
    .ConfigureServices(services => services
        .Configure<TelemetryConfiguration>(configuration => configuration.SetAzureTokenCredential(tokenCredential)).AddApplicationInsightsTelemetryWorkerService()
        .AddSingleton(provider => new BlobContainerClient(provider.GetRequiredService<IConfiguration>().GetValue<Uri>("BLOB_CONTAINER_URI"), tokenCredential))
        .AddSingleton<WorkspaceService>()
        .AddSingleton(_ => new ArmClient(tokenCredential))
        .AddMemoryCache()
        .AddHttpClient(nameof(WorkspaceService), client =>
        {
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new(MediaTypeNames.Application.Json));
            client.DefaultRequestHeaders.Add("Log-Type", "ContainerLog");
            client.DefaultRequestHeaders.Add("time-generated-field", nameof(Entity.TimeGenerated));
        })
        .AddTransientHttpErrorPolicy(policy => policy.WaitAndRetryAsync(3, retry => TimeSpan.FromMilliseconds(250 * retry + 1))))
    .ConfigureFunctionsWorkerDefaults(app => app.AddApplicationInsights().AddApplicationInsightsLogger())
    .Build()
    .RunAsync();