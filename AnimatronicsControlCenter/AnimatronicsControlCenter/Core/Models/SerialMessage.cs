using System.Text.Json.Serialization;

namespace AnimatronicsControlCenter.Core.Models
{
    public class SerialMessage
    {
        [JsonPropertyName("cmd")]
        public string Command { get; set; } = string.Empty;

        [JsonPropertyName("payload")]
        public object? Payload { get; set; }
    }
}

