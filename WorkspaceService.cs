using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.OperationalInsights;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Mime;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ContainerLogExporter;

internal class WorkspaceService
{
    private readonly ILogger<WorkspaceService> logger;
    private readonly IHttpClientFactory factory;
    private readonly IMemoryCache cache;
    private readonly IConfiguration configuration;
    private readonly ArmClient arm;

    private const string AuthorizationScheme = "SharedKey";
    private const string NamespaceTag = "namespace";

    public WorkspaceService(ILogger<WorkspaceService> logger, IHttpClientFactory factory, IMemoryCache cache, IConfiguration configuration, ArmClient arm)
    {
        this.logger = logger;
        this.factory = factory;
        this.cache = cache;
        this.configuration = configuration;
        this.arm = arm;
    }

    public async Task SendLogs(string @namespace, Entity[] entities)
    {
        using var scope = logger.BeginScope(@namespace);

        logger.LogDebug(Events.WorkspaceDataLookup, "Looking up workspace data for namespace {namespace}", @namespace);
        (string? workspaceId, string? workspaceKey) = await cache.GetOrCreateAsync(@namespace, async entry =>
        {
            (string? workspaceId, string? workspaceKey) data = default;
            SubscriptionResource subscription = arm.GetSubscriptionResource(new($"/subscriptions/{configuration.GetValue<string>("SUBSCRIPTION_ID")}"));
            await foreach (var workspace in subscription.GetWorkspacesAsync())
            {
                if (workspace.Data.Tags.ContainsKey(NamespaceTag) && workspace.Data.Tags[NamespaceTag].Equals(@namespace, StringComparison.Ordinal))
                {
                    var sharedKeys = await workspace.GetSharedKeysSharedKeyAsync();
                    data = (workspace.Data.CustomerId, sharedKeys.Value.PrimarySharedKey);
                }
            }
            entry.SetValue(data).SetAbsoluteExpiration(TimeSpan.FromMinutes(1));
            return data;
        });
        if (workspaceId is not { Length: > 0 } || workspaceKey is not { Length: > 0 })
        {
            logger.LogError(Events.WorkspaceDataNotFound, "Failed to find workspace data for namespace {namespace}", @namespace);
            return;
        }
        logger.LogDebug(Events.WorkspaceDataFound, "Found workspace data for namespace {namespace}: {id}/{key}", @namespace, $"{workspaceId[..8]}-***", $"***-{workspaceKey[^8..]}");

        string json = JsonSerializer.Serialize(entities);
        string timestamp = DateTime.UtcNow.ToString("r");

        using HttpClient client = factory.CreateClient(nameof(WorkspaceService));
        using HttpContent httpContent = new StringContent(json, Encoding.UTF8);
        client.BaseAddress = new Uri($"https://{workspaceId}.ods.opinsights.azure.com/api/logs?api-version=2016-04-01");
        httpContent.Headers.ContentType = new(MediaTypeNames.Application.Json);
        client.DefaultRequestHeaders.Authorization = new(AuthorizationScheme, GetAuthorizationHeaderValue(json, workspaceId, workspaceKey, timestamp));
        client.DefaultRequestHeaders.Add("x-ms-date", timestamp);

        logger.LogInformation(Events.WorkspaceSendLogs, "Sending {count} records to workspace for namespace {namespace}", entities.Length, @namespace);
        try
        {
            await client.PostAsync(string.Empty, httpContent);
        }
        catch (Exception exception)
        {
            logger.LogError(Events.WorkspaceSendLogsHttpError, exception, "Error sending logs: {message}", exception.Message);
        }
    }

    private static string GetAuthorizationHeaderValue(string jsonPayload, string workspaceId, string workspaceKey, string timestamp)
    {
        byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonPayload);
        string signPayload = string.Join("\n", new[] { "POST", jsonBytes.Length.ToString(), MediaTypeNames.Application.Json, $"x-ms-date:{timestamp}", "/api/logs" });
        string signature = CreateSignature(signPayload, workspaceKey);
        return $"{workspaceId}:{signature}";
    }

    private static string CreateSignature(string message, string secret)
    {
        ASCIIEncoding encoding = new();
        byte[] keyByte = Convert.FromBase64String(secret);
        byte[] messageBytes = encoding.GetBytes(message);
        using (var hmacsha256 = new HMACSHA256(keyByte))
        {
            byte[] hash = hmacsha256.ComputeHash(messageBytes);
            return Convert.ToBase64String(hash);
        }
    }
}