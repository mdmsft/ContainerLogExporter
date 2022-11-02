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
        string? workspaceKey = await secretService.GetWorkspaceKey(@namespace);
        if (workspaceKey is null)
        {
            logger.LogError(Events.WorkspaceKeyNotFound, "Cannot find workspace key for workspace {workspace}", @namespace);
            return;
        }
        using WorkspaceHttpClient httpClient = new(@namespace, workspaceKey);
        logger.LogInformation(Events.WorkspaceSendLogs, "Sending {count} records to {workspace} workspace", records.Length, @namespace);
        await httpClient.SendLogs(records);
    }
}