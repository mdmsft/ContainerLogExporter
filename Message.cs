using System.Text.Json.Serialization;

namespace ContainerLogExporter;

internal class Message
{
    [JsonPropertyName("records")]
    public Record[] Records { get; set; }
}
