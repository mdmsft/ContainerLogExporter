using System.Text.Json.Serialization;

namespace ContainerLogExporter;

internal class Model
{
    [JsonPropertyName(nameof(TimeGenerated))]
    public string TimeGenerated { get; set; } = string.Empty;


    [JsonPropertyName(nameof(Computer))]
    public string Computer { get; set; } = string.Empty;


    [JsonPropertyName(nameof(ContainerId))]
    public string ContainerId { get; set; } = string.Empty;


    [JsonPropertyName(nameof(ContainerName))]
    public string ContainerName { get; set; } = string.Empty;


    [JsonPropertyName(nameof(PodName))]
    public string PodName { get; set; } = string.Empty;


    [JsonPropertyName(nameof(PodNamespace))]
    public string PodNamespace { get; set; } = string.Empty;


    [JsonPropertyName(nameof(LogMessage))]
    public string LogMessage { get; set; } = string.Empty;


    [JsonPropertyName(nameof(LogSource))]
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
