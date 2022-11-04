using Azure.ResourceManager;
using Azure.ResourceManager.OperationalInsights;
using Azure.ResourceManager.OperationalInsights.Models;
using Azure.ResourceManager.Resources;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ContainerLogExporter;

internal class WorkspaceService
{
    private readonly ArmClient armClient;
    private readonly IMemoryCache memoryCache;
    private readonly ILogger<WorkspaceService> logger;
    private readonly TelemetryClient telemetryClient;
    private const string NamespaceTag = "namespace";

    public WorkspaceService(ArmClient armClient, IMemoryCache memoryCache, ILogger<WorkspaceService> logger, TelemetryClient telemetryClient)
    {
        this.armClient = armClient;
        this.memoryCache = memoryCache;
        this.logger = logger;
        this.telemetryClient = telemetryClient;
    }

    public async Task SendLogs(string @namespace, Entity[] entities)
    {
        using var scope = logger.BeginScope(@namespace);
        logger.LogInformation(Events.WorkspaceLookup, "Looking up workspace for namespace {namespace}", @namespace);

        (string? workspaceId, string? workspaceKey) = await memoryCache.GetOrCreateAsync<(string?, string?)>(@namespace, async (entity) =>
        {
            logger.LogInformation(Events.WorkspaceCacheMiss, "Cache miss looking up for namespace {namespace}", @namespace);
            SubscriptionResource subscription = await armClient.GetDefaultSubscriptionAsync();
            await foreach (WorkspaceResource workspace in subscription.GetWorkspacesAsync())
            {
                telemetryClient.TrackTrace($"Loading workspace {workspace.Id}", SeverityLevel.Verbose);
                await workspace.GetAsync();
                if (workspace.Data.Tags.ContainsKey(NamespaceTag) && workspace.Data.Tags[NamespaceTag] == @namespace)
                {
                    telemetryClient.TrackTrace($"Found workspace {workspace.Id.Name} for namespace {@namespace}", SeverityLevel.Verbose);
                    SharedKeys sharedKeys = await workspace.GetSharedKeysSharedKeyAsync();
                    telemetryClient.TrackTrace($"Found shared keys in workspace {workspace.Id.Name}", SeverityLevel.Verbose);
                    string workspaceId = workspace.Data.CustomerId;
                    string sharedKey = sharedKeys.PrimarySharedKey;
                    logger.LogInformation(Events.WorkspaceFound, "Found workspace for namespace {namespace}: CustomerId = {customerId}, PSK = {sharedKey}", @namespace, $"{workspaceId[..8]}-***", $"***-{sharedKey[^8..]}");
                    return (workspaceId, sharedKey);
                }
            }
            return (default, default);
        });
        
        if (workspaceId is null || workspaceKey is null)
        {
            logger.LogError(Events.WorkspaceNotFound, "Cannot find workspace for namespace {namespace}", @namespace);
            return;
        }
        
        using WorkspaceHttpClient httpClient = new(workspaceId, workspaceKey);
        logger.LogInformation(Events.WorkspaceSendLogs, "Sending {count} records to workspace for namespace {namespace}", entities.Length, @namespace);
        await httpClient.SendLogs(entities);
    }
}