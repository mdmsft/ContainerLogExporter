using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.ApplicationInsights;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace ContainerLogExporter;

internal class Function
{
    private readonly WorkspaceService workspaceService;
    private readonly TelemetryClient telemetryClient;
    private readonly BlobContainerClient blobContainerClient;
    private readonly string[] ignoredNamespaces;
    private readonly string[] defaultIgnoredNamespaces = new[]
    {
        "default",
        "gatekeeper-system",
        "kube-node-lease",
        "kube-system",
        "kube-public",
    };
    private readonly JsonDocumentOptions jsonDocumentOptions = new() { AllowTrailingCommas = true };

    public Function(IConfiguration configuration, WorkspaceService workspaceService, TelemetryClient telemetryClient, BlobContainerClient blobContainerClient)
    {
        this.workspaceService = workspaceService;
        this.telemetryClient = telemetryClient;
        this.blobContainerClient = blobContainerClient;
        ignoredNamespaces = configuration.GetValue("IgnoredNamespaces", defaultIgnoredNamespaces);
    }

    [Function(nameof(Function))]
    public async Task Run([EventHubTrigger("%EVENT_HUB_NAME%", Connection = "EventHub")] string[] messages, FunctionContext functionContext, CancellationToken cancellationToken)
    {
        ILogger<Function> logger = functionContext.GetLogger<Function>();
        using var _ = logger.BeginScope(string.Empty);
        foreach (string message in messages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string encodedJson = HttpUtility.JavaScriptStringEncode(message);
            string sanitizedJson = encodedJson.Replace("\\\"", "\"");
            string json = Regex.Replace(sanitizedJson, """(?<="LogMessage":)\s+(?!")(.*?)(?!")(?=,\s"LogSource")""", "\"$1\"", RegexOptions.Multiline);
            try
            {
                Model[]? records = JsonSerializer.Deserialize<Message>(json)?.Records;
                if (records is not { Length: >0 })
                {
                    logger.LogWarning(Events.MessageIsEmpty, "Message is empty {message}", json);
                    continue;
                }
                logger.LogInformation(Events.RecordsFound, "Found {count} records in the message", records.Length);
                foreach (var group in records.GroupBy(record => record.PodNamespace).Where(group => Array.IndexOf(ignoredNamespaces, group.Key) == -1))
                {
                    await workspaceService.SendLogs(group.Key, group.Select(g => g.ToEntity()).ToArray());
                }
            }
            catch (JsonException exception)
            {
                await Task.WhenAll(new[]
                {
                    UploadBlob($"{functionContext.InvocationId}-org", message),
                    UploadBlob($"{functionContext.InvocationId}-enc", encodedJson),
                    UploadBlob($"{functionContext.InvocationId}-cln", sanitizedJson),
                    UploadBlob(functionContext.InvocationId, json)
                });
                logger.LogError(Events.MessageCannotBeParsed, exception, "Error parsing message. Details can be found in corresponding blobs.");
            }
        }
    }

    private async Task UploadBlob(string name, string data)
    {
        using Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(data));
        await blobContainerClient.UploadBlobAsync(name, stream);
    }
}
