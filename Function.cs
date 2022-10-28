using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ContainerLogExporter;

public class Function
{
    private readonly ILogger logger;

    public Function(ILoggerFactory loggerFactory)
    {
        logger = loggerFactory.CreateLogger<Function>();
    }

    [Function(nameof(Function))]
    public void Run([EventHubTrigger("default", Connection = "Default")] string[] input)
    {
        logger.LogInformation("Event Hub messages: {0}", string.Join(",", input));
    }
}