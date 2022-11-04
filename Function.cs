using Azure.Storage.Blobs;
using Microsoft.ApplicationInsights;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ContainerLogExporter;

internal class Function
{
    private readonly ILogger logger;
    private readonly WorkspaceService workspaceService;
    private readonly TelemetryClient telemetryClient;
    private readonly string[] ignoredNamespaces;
    private readonly string[] defaultIgnoredNamespaces = new[]
    {
        "default",
        "gatekeeper-system",
        "kube-node-lease",
        "kube-system",
        "kube-public",
    };

    public Function(IConfiguration configuration, WorkspaceService workspaceService, FunctionContext functionContext, TelemetryClient telemetryClient)
    {
        logger = functionContext.GetLogger<Function>();
        this.workspaceService = workspaceService;
        this.telemetryClient = telemetryClient;
        ignoredNamespaces = configuration.GetValue("IgnoredNamespaces", defaultIgnoredNamespaces);
    }

    [Function(nameof(Function))]
    public async Task Run([EventHubTrigger("%EVENT_HUB_NAME%", Connection = "EventHub")] string[] messages, [Blob("messages")] BlobContainerClient blobContainerClient, CancellationToken cancellationToken)
    {
        foreach (string message in messages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(message));
            await blobContainerClient.CreateIfNotExistsAsync();
            await blobContainerClient.UploadBlobAsync($"{DateTime.UtcNow.ToString("yyyy-MM-dd-hh-mm-ss")}.json", stream);
            string msg = Regex.Replace(message.Replace(Environment.NewLine, string.Empty), """(?<="LogMessage":)\s+(?!")(.*?)(?!")(?=,\s"LogSource")""", "\"$1\"", RegexOptions.Multiline);
            try
            {
                using JsonDocument document = JsonDocument.Parse(msg);
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
                    return;
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
