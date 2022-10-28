using Azure.Data.Tables;
using Azure.Identity;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ContainerLogExporter;

public class Function
{
    private readonly ILogger logger;
    private readonly TableServiceClient tableServiceClient;

    public Function(ILoggerFactory loggerFactory, IConfiguration configuration)
    {
        logger = loggerFactory.CreateLogger<Function>();
        tableServiceClient = new TableServiceClient(configuration.GetValue<Uri>("TABLE_SERVICE_URI"), new ManagedIdentityCredential());
    }

    [Function(nameof(Function))]
    public async Task Run([EventHubTrigger("%EVENT_HUB_NAME%", Connection = "EventHub")] string[] messages)
    {
        foreach (var messaage in messages)
        {
            Message msg = JsonSerializer.Deserialize<Message>(messaage);
            foreach (var record in msg.Records)
            {
                logger.LogInformation("PodNamespace: {PodNamespace}, ContainerName: {ContainerName}, TimeGenerated: {TimeGenerated}, LogMessage: {LogMessage}, LogSource: {LogSource}, ContainerId: {ContainerId}, Computer: {Computer}, PodName: {PodName}",
                    record.PodNamespace, record.ContainerName, record.TimeGenerated, record.LogMessage, record.LogSource, record.ContainerId, record.Computer, record.PodName);
                TableClient tableClient = tableServiceClient.GetTableClient(record.PodNamespace.Replace("-", ""));
                await tableClient.CreateIfNotExistsAsync();
                TableEntity entity = new(record.ContainerName, record.TimeGenerated)
                {
                    { "Message", record.LogMessage },
                    { "Source", record.LogSource },
                    { "Container", record.ContainerId },
                    { "Pod",  record.PodName },
                    { "Node",  record.Computer },
                };
                await tableClient.AddEntityAsync(entity);
            }
        }
    }
}
