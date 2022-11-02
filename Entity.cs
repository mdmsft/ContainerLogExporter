namespace ContainerLogExporter;

internal class Entity
{
    public string TimeGenerated { get; set; } = string.Empty;

    public string PodName { get; set; } = string.Empty;

    public string LogMessage { get; set; } = string.Empty;

    public string LogSource { get; set; } = string.Empty;

    public string ContainerId { get; set; } = string.Empty;

    public string ContainerName { get; set; } = string.Empty;

    public string NodeName { get; set; } = string.Empty;
}
