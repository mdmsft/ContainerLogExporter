using Microsoft.Extensions.Logging;

namespace ContainerLogExporter;

internal class WorkspaceService
{
    private readonly SecretService secretService;

    private readonly ILogger<WorkspaceService> logger;

    public WorkspaceService(SecretService secretService, ILogger<WorkspaceService> logger)
    {
        this.secretService = secretService;
        this.logger = logger;
    }

    public async Task SendLogs(string @namespace, Record[] records)
    {
        using var scope = logger.BeginScope<string>(@namespace);
        (string? id, string? key) = await secretService.GetWorkspaceInfo(@namespace);
        if (id is null || key is null)
        {
            logger.LogError(Events.WorkspaceKeyNotFound, "Cannot find workspace named {workspace}", @namespace);
            return;
        }
        using WorkspaceHttpClient httpClient = new(id, key);
        logger.LogInformation(Events.WorkspaceSendLogs, "Sending {count} records to {workspace} workspace", records.Length, @namespace);
        await httpClient.SendLogs(records);
    }
}