using System.Net.Mime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;

namespace ContainerLogExporter;

internal static class HostBuilderContextExtensions
{
    private const string WorkspacePrefix = "workspace:";

    private const string LogName = "ContainerLog";

    public static IServiceCollection AddWorkspaceHttpClients(this IServiceCollection services)
    {
        IConfiguration configuration = services.BuildServiceProvider().GetRequiredService<IConfiguration>();
        foreach (KeyValuePair<string, string?> kvp in configuration.AsEnumerable())
        {
            if (kvp.Key.StartsWith(WorkspacePrefix) && kvp.Value is { Length: > 0 })
            {
                services.AddHttpClient(kvp.Key.TrimStart(WorkspacePrefix.ToCharArray()), client =>
                {
                    client.BaseAddress = new Uri($"https://{kvp.Value}.ods.opinsights.azure.com/api/logs?api-version=2016-04-01");
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new(MediaTypeNames.Application.Json));
                    client.DefaultRequestHeaders.Add("Log-Type", LogName);
                    client.DefaultRequestHeaders.Add("time-generated-field", nameof(Entity.TimeGenerated));
                }).AddTransientHttpErrorPolicy(policy => policy.WaitAndRetryAsync(3, retry => TimeSpan.FromSeconds(retry + 1)));
            }
        }
        return services;
    }
}