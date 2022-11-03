using Microsoft.ApplicationInsights;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ContainerLogExporter;

internal class Function
{
    private readonly ILogger logger;
    private readonly ILoggerFactory loggerFactory;
    private readonly IConfiguration configuration;
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

    public Function(ILoggerFactory loggerFactory, IConfiguration configuration, WorkspaceService workspaceService, TelemetryClient telemetryClient)
    {
        logger = loggerFactory.CreateLogger<Function>();
        this.loggerFactory = loggerFactory;
        this.configuration = configuration;
        this.workspaceService = workspaceService;
        this.telemetryClient = telemetryClient;
        ignoredNamespaces = configuration.GetValue("IgnoredNamespaces", defaultIgnoredNamespaces);
    }

    [Function(nameof(Function))]
    public async Task Run([EventHubTrigger("%EVENT_HUB_NAME%", Connection = "EventHub")] string[] messages, CancellationToken cancellationToken)
    {
        foreach (string message in messages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using JsonDocument document = JsonDocument.Parse(message);
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
                    logger.LogWarning(Events.MessageIsEmpty, "Message is empty or has invalid records: {message}", message);
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
                logger.LogError(Events.MessageCannotBeParsed, exception, "Error parsing message: {message}", message);
            }
        }
    }
}
