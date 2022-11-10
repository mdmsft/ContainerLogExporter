using Microsoft.Extensions.Logging;
using System.Net.Mime;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ContainerLogExporter;

internal class WorkspaceService
{
    private readonly ILogger<WorkspaceService> logger;
    private readonly IHttpClientFactory clientFactory;
    private readonly SecretService secretService;

    private const string AuthorizationScheme = "SharedKey";

    public WorkspaceService(ILogger<WorkspaceService> logger, IHttpClientFactory clientFactory, SecretService secretService)
    {
        this.logger = logger;
        this.clientFactory = clientFactory;
        this.secretService = secretService;
    }

    public async Task SendLogs(string @namespace, Entity[] entities)
    {
        using var scope = logger.BeginScope(@namespace);

        logger.LogInformation(Events.WorkspaceKeyLookup, "Looking up workspace key for namespace {namespace}", @namespace);
        string? workspaceKey = await secretService.GetSecret(@namespace);
        if (workspaceKey is null)
        {
            logger.LogError(Events.WorkspaceKeyNotFound, "Failed to find workspace key for namespace {namespace}", @namespace);
            return;
        }
        logger.LogInformation(Events.WorkspaceKeyFound, "Found workspace key for namespace {namespace}: {id}", @namespace, $"***-{workspaceKey[^8..]}");

        string json = JsonSerializer.Serialize(entities);

        using HttpClient client = clientFactory.CreateClient(@namespace);
        using HttpContent httpContent = new StringContent(json, Encoding.UTF8);
        
        string timestamp = DateTime.UtcNow.ToString("r");
        string workspaceId = client.BaseAddress!.Host.Split('.')[0];
        
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

    private string GetAuthorizationHeaderValue(string jsonPayload, string workspaceId, string workspaceKey, string timestamp)
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