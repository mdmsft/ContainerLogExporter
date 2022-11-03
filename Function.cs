using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ContainerLogExporter;

internal class Function
{
    private readonly ILogger logger;
    private readonly WorkspaceService workspaceService;
    private readonly string[] ignoredNamespaces;
    private readonly string[] defaultIgnoredNamespaces = new[]
    {
        "default",
        "gatekeeper-system",
        "kube-node-lease",
        "kube-system",
        "kube-public",
    };

    public Function(ILoggerFactory loggerFactory, IConfiguration configuration, WorkspaceService workspaceService)
    {
        logger = loggerFactory.CreateLogger<Function>();
        this.workspaceService = workspaceService;
        ignoredNamespaces = configuration.GetValue("IgnoredNamespaces", defaultIgnoredNamespaces);
    }

    [Function(nameof(Function))]
    public async Task Run([EventHubTrigger("%EVENT_HUB_NAME%", Connection = "EventHub")] string[] messages, CancellationToken cancellationToken)
    {
        foreach (string message in messages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogInformation(4004, "Input message: {message}", message);
            try
            {
                Message? msg = JsonSerializer.Deserialize<Message>(message);
                if (msg is null || msg is { Records.Length: 0 })
                {
                    logger.LogWarning(Events.MessageIsNullOrEmpty, "Message is null or empty: {message}", message);
                    return;
                }
                foreach (var group in msg.Records.GroupBy(record => record.PodNamespace).Where(group => Array.IndexOf(ignoredNamespaces, group.Key) == -1))
                {
                    await workspaceService.SendLogs(group.Key, group.Select(g => g.ToEntity()).ToArray());
                }
            }
            catch (JsonException exception)
            {
                logger.LogError(Events.MessageCannotBeDeserialized, exception, "Error deserializing message: {message}", message);
            }
        }
    }
}
