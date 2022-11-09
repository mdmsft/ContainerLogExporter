using System.Text.Json.Serialization;

namespace ContainerLogExporter;

internal class Model
{
    public string TimeGenerated { get; set; } = string.Empty;

    public string Computer { get; set; } = string.Empty;

    public string ContainerId { get; set; } = string.Empty;

    public string ContainerName { get; set; } = string.Empty;

    public string PodName { get; set; } = string.Empty;

    public string PodNamespace { get; set; } = string.Empty;

    public string LogMessage { get; set; } = string.Empty;

    public string LogSource { get; set; } = string.Empty;

    public Entity ToEntity() => new()
    {
        ContainerId = ContainerId,
        ContainerName = ContainerName,
        LogMessage = LogMessage,
        LogSource = LogSource,
        NodeName = Computer,
        PodName = PodName,
        TimeGenerated = TimeGenerated
    };
}
