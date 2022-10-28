using System.Text.Json.Serialization;

namespace ContainerLogExporter;

internal class Record
{
    [JsonPropertyName(nameof(TimeGenerated))]
    public string TimeGenerated { get; set; }


    [JsonPropertyName(nameof(Computer))]
    public string Computer { get; set; }


    [JsonPropertyName(nameof(ContainerId))]
    public string ContainerId { get; set; }


    [JsonPropertyName(nameof(ContainerName))]
    public string ContainerName { get; set; }


    [JsonPropertyName(nameof(PodName))]
    public string PodName { get; set; }


    [JsonPropertyName(nameof(PodNamespace))]
    public string PodNamespace { get; set; }


    [JsonPropertyName(nameof(LogMessage))]
    public string LogMessage { get; set; }


    [JsonPropertyName(nameof(LogSource))]
    public string LogSource { get; set; }
}
