using Azure.Storage.Blobs;
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
    private readonly HashSet<string> ignoredNamespaces;
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
        ignoredNamespaces = new(configuration.GetValue<string[]>("IgnoredNamespaces", defaultIgnoredNamespaces));
    }

    [Function(nameof(Function))]
    public async Task Run([EventHubTrigger("%EVENT_HUB_NAME%", Connection = "EventHub")] string[] messages, FunctionContext functionContext, CancellationToken cancellationToken)
    {
        ILogger<Function> logger = functionContext.GetLogger<Function>();
        using var _ = logger.BeginScope(functionContext.InvocationId);
        foreach (string message in messages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string json = Regex.Replace(HttpUtility.JavaScriptStringEncode(message).Replace("\\\"", "\""), """(?<="LogMessage":)\s+(?!")(.*?)(?!")(?=,\s"LogSource")""", "\"$1\"", RegexOptions.Multiline);
            try
            {
                Model[]? records = JsonSerializer.Deserialize<Message>(json)?.Records;
                if (records is not { Length: > 0 })
                {
                    logger.LogWarning(Events.MessageIsEmpty, "Message is empty: {message}", json);
                    continue;
                }
                Model[] valuableRecords = records.Where(record => !ignoredNamespaces.Contains(record.PodNamespace)).ToArray();
                logger.LogInformation(Events.RecordsFound, "Found {total} records in the message, but only {valuable} are valuable", records.Length, valuableRecords.Length);
                foreach (var group in valuableRecords.GroupBy(record => record.PodNamespace))
                {
                    await workspaceService.SendLogs(group.Key, group.Select(g => g.ToEntity()).ToArray());
                }
            }
            catch (JsonException exception)
            {
                using Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
                await blobContainerClient.UploadBlobAsync($"{functionContext.InvocationId}.json", stream);
                logger.LogError(Events.MessageCannotBeParsed, exception, "Error parsing message. Details can be found in corresponding blob.");
            }
        }
    }
}
