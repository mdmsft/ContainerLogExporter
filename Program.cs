using Azure.Identity;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services => services.AddApplicationInsightsTelemetryWorkerService().Configure<TelemetryConfiguration>(configuration => configuration.SetAzureTokenCredential(new ManagedIdentityCredential())))
    .Build();

host.Run();