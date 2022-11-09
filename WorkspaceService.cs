using System.Net;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ContainerLogExporter;

internal class WorkspaceService
{
    private readonly ILogger<WorkspaceService> logger;
    private readonly IConfiguration configuration;
    private readonly SecretService secretService;
    private readonly TelemetryClient telemetryClient;

    public WorkspaceService(ILogger<WorkspaceService> logger, IConfiguration configuration, SecretService secretService, TelemetryClient telemetryClient)
    {
        this.logger = logger;
        this.configuration = configuration;
        this.secretService = secretService;
        this.telemetryClient = telemetryClient;
    }

    public async Task SendLogs(string @namespace, Entity[] entities)
    {
        using var scope = logger.BeginScope(@namespace);

        logger.LogInformation(Events.WorkspaceIdLookup, "Looking up workspace ID for namespace {namespace}", @namespace);
        string? workspaceId = configuration.GetValue<string?>(@namespace);
        if (workspaceId is null)
        {
            logger.LogError(Events.WorkspaceIdNotFound, "Failed to find workspace ID for namespace {namespace}", @namespace);
            return;
        }
        logger.LogInformation(Events.WorkspaceIdFound, "Found workspace ID for namespace {namespace}: {id}", @namespace, $"{workspaceId[..8]}-***");

        logger.LogInformation(Events.WorkspaceKeyLookup, "Looking up workspace key for namespace {namespace}", @namespace);
        string? workspaceKey = await secretService.GetSecret(@namespace);
        if (workspaceKey is null)
        {
            logger.LogError(Events.WorkspaceKeyNotFound, "Failed to find workspace key for namespace {namespace}", @namespace);
            return;
        }
        logger.LogInformation(Events.WorkspaceKeyFound, "Found workspace key for namespace {namespace}: {id}", @namespace, $"***-{workspaceKey[^8..]}");

        using WorkspaceHttpClient httpClient = new(workspaceId, workspaceKey);
        telemetryClient.TrackEvent("dns", new Dictionary<string, string>
        {
            { "Host", httpClient.BaseAddress.Host },
            { "Addresses", string.Join(',', Dns.GetHostEntry(httpClient.BaseAddress.Host).AddressList.Select(address => address.ToString())) }
        });
        logger.LogInformation(Events.WorkspaceSendLogs, "Sending {count} records to workspace for namespace {namespace}", entities.Length, @namespace);
        try
        {
            await httpClient.SendLogs(entities);
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(Events.WorkspaceSendLogsHttpError, exception, "Error sending logs: {message}", exception.Message);
        }
    }
}