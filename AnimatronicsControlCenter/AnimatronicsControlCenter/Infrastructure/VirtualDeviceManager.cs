using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AnimatronicsControlCenter.Infrastructure
{
    public class VirtualDeviceManager
    {
        private readonly Dictionary<int, VirtualDevice> _devices = new();

        public VirtualDeviceManager()
        {
            // Initialize some virtual devices
            for (int i = 1; i <= 20; i++)
            {
                _devices[i] = new VirtualDevice { Id = i, IsOnline = true };
            }
        }

        public string? ProcessCommand(string jsonCommand)
        {
            try
            {
                var message = JsonSerializer.Deserialize<VirtualMessage>(jsonCommand);
                if (message == null) return null;

                if (_devices.TryGetValue(message.Id, out var device) && device.IsOnline)
                {
                    // Simulate processing delay
                    // In a real scenario, this would be async, but for this simulation helper, we return string.
                    // The caller (SerialService) handles the async delay.

                    if (message.Command == "ping")
                    {
                        var response = new
                        {
                            id = device.Id,
                            status = "pong",
                            payload = new { message = "Online" }
                        };
                        return JsonSerializer.Serialize(response);
                    }
                    // Handle other commands here
                }
            }
            catch
            {
                // Ignore invalid JSON
            }
            return null;
        }
    }

    public class VirtualDevice
    {
        public int Id { get; set; }
        public bool IsOnline { get; set; }
    }

    public class VirtualMessage
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("cmd")]
        public string Command { get; set; } = string.Empty;

        [JsonPropertyName("payload")]
        public object? Payload { get; set; }
    }
}

