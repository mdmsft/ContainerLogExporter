using System.Text.Json.Serialization;

namespace ContainerLogExporter;

internal class Message
{
    [JsonPropertyName("records")]
    public Model[] Records { get; set; } = Array.Empty<Model>();
}
