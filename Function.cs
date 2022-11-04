using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.ApplicationInsights;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

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
            // string name = $"{DateTime.UtcNow.ToString("yyyy-MM-dd-hh-mm-ss")}.json";
            // byte[] buffer = Encoding.UTF8.GetBytes(message);
            // using Stream stream = new MemoryStream(buffer);
            // BlobContentInfo blobContentInfo = await blobContainerClient.UploadBlobAsync(name, stream);
            // logger.LogInformation(4004, "Uploaded blob {name}, size: {size}, hash: {hash}", name, buffer.Length, blobContentInfo.ContentHash);
            string msg = Regex.Replace(message.Replace(Environment.NewLine, string.Empty), """(?<="LogMessage":)\s+(?!")(.*?)(?!")(?=,\s"LogSource")""", "\"$1\"", RegexOptions.Multiline);
            try
            {
                using JsonDocument document = JsonDocument.Parse(msg, jsonDocumentOptions);
                JsonElement root = document.RootElement.GetProperty("records");
                List<Model> records = new();
                foreach (JsonElement record in root.EnumerateArray())
                {
                    try
                    {
                        Model? model = JsonSerializer.Deserialize<Model>(record);
                        if (model is not null)
                        {
                            records.Add(model);
                        }
                    }
                    catch (JsonException exception)
                    {
                        logger.LogError(Events.RecordCannotBeDeserialized, exception, "Error deserializing record: {record}", record.ToString());
                        continue;
                    }
                }
                if (records.Count == 0)
                {
                    logger.LogWarning(Events.MessageIsEmpty, "Message is empty or has invalid records: {message}", msg);
                    continue;
                }
                logger.LogInformation(Events.RecordsFound, "Found {count} valid records in the message", records.Count);
                foreach (var group in records.GroupBy(record => record.PodNamespace).Where(group => Array.IndexOf(ignoredNamespaces, group.Key) == -1))
                {
                    await workspaceService.SendLogs(group.Key, group.Select(g => g.ToEntity()).ToArray());
                }
            }
            catch (JsonException exception)
            {
                logger.LogError(Events.MessageCannotBeParsed, exception, "Error parsing message: {message}", Convert.ToBase64String(Encoding.UTF8.GetBytes(message)));
            }
        }
    }
}
