using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Text.Json;

namespace ContainerLogExporter;

internal class Function
{
    private readonly ILogger logger;
    private readonly WorkspaceService workspaceService;
    private readonly string[] ignoredNamespaces = new[]
    {
        "aad-pod-identity",
        "argocd",
        "calico-system",
        "default",
        "gatekeeper-system",
        "kube-node-lease",
        "kube-system",
        "kube-public",
        "kyverno",
        "service-operator-system",
        "tigera-operator"
    };

    public Function(ILoggerFactory loggerFactory, WorkspaceService workspaceService)
    {
        logger = loggerFactory.CreateLogger<Function>();
        this.workspaceService = workspaceService;
    }

    [Function(nameof(Function))]
    public async Task Run([EventHubTrigger("%EVENT_HUB_NAME%", Connection = "EventHub")] string[] messages)
    {
        foreach (var message in messages)
        {
            try
            {
                Message? msg = JsonSerializer.Deserialize<Message>(message);
                if (msg is null || msg is { Records: { Length: 0}})
                {
                    logger.LogWarning(Events.MessageIsNullOrEmpty, "Message is null or empty: {message}", message);
                    return;
                }
                foreach (var group in msg.Records.GroupBy(record => record.PodNamespace).Where(key => Array.IndexOf(ignoredNamespaces, key) == -1))
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
