using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using AnimatronicsControlCenter.Core.Models;

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
                    else if (message.Command == "get_files")
                    {
                        var root = device.FileSystem;
                        var response = new
                        {
                            id = device.Id,
                            status = "ok",
                            payload = root
                        };
                        return JsonSerializer.Serialize(response);
                    }
                    else if (message.Command == "get_file")
                    {
                        var pathElement = ((JsonElement)message.Payload).GetProperty("path");
                        string path = pathElement.GetString() ?? "";
                        var content = device.GetFileContent(path);
                        var response = new
                        {
                            id = device.Id,
                            status = content != null ? "ok" : "error",
                            payload = new { path = path, content = content ?? "File not found" }
                        };
                        return JsonSerializer.Serialize(response);
                    }
                    else if (message.Command == "save_file")
                    {
                        var payload = (JsonElement)message.Payload;
                        string path = payload.GetProperty("path").GetString() ?? "";
                        string content = payload.GetProperty("content").GetString() ?? "";
                        device.SaveFileContent(path, content);
                        var response = new
                        {
                            id = device.Id,
                            status = "ok",
                            payload = new { message = "File saved" }
                        };
                        return JsonSerializer.Serialize(response);
                    }
                    else if (message.Command == "verify_file")
                    {
                        var payload = (JsonElement)message.Payload;
                        string path = payload.GetProperty("path").GetString() ?? "";
                        string content = payload.GetProperty("content").GetString() ?? "";
                        bool match = device.GetFileContent(path) == content;
                        var response = new
                        {
                            id = device.Id,
                            status = "ok",
                            payload = new { match = match }
                        };
                        return JsonSerializer.Serialize(response);
                    }
                    else if (message.Command == "motion_ctrl")
                    {
                        var payload = (JsonElement)message.Payload;
                        string action = payload.GetProperty("action").GetString() ?? "";
                        
                        // Update device state based on action
                        switch(action)
                        {
                            case "play": device.State = MotionState.Playing; break;
                            case "stop": device.State = MotionState.Stopped; break;
                            case "pause": device.State = MotionState.Paused; break;
                        }

                        var response = new
                        {
                            id = device.Id,
                            status = "ok",
                            payload = new { state = device.State.ToString() }
                        };
                        return JsonSerializer.Serialize(response);
                    }
                    else if (message.Command == "move")
                    {
                        // Simulate move
                         var response = new
                        {
                            id = device.Id,
                            status = "ok",
                            payload = new { message = "Moved" }
                        };
                        return JsonSerializer.Serialize(response);
                    }
                }
            }
            catch
            {
                // Ignore invalid JSON or schema mismatches for simplicity
            }
            return null;
        }
    }

    public class VirtualDevice
    {
        public int Id { get; set; }
        public bool IsOnline { get; set; }
        public MotionState State { get; set; } = MotionState.Idle;

        public List<FileSystemItem> FileSystem { get; private set; }
        private Dictionary<string, string> _fileContents = new();

        public VirtualDevice()
        {
            // Init mock file system
            FileSystem = new List<FileSystemItem>
            {
                new FileSystemItem
                {
                    Name = "motions",
                    Path = "motions",
                    IsDirectory = true,
                    Children = new System.Collections.ObjectModel.ObservableCollection<FileSystemItem>
                    {
                        new FileSystemItem { Name = "dance.json", Path = "motions/dance.json", IsDirectory = false, Size = 1024 },
                        new FileSystemItem { Name = "wave.json", Path = "motions/wave.json", IsDirectory = false, Size = 512 }
                    }
                },
                new FileSystemItem
                {
                    Name = "config",
                    Path = "config",
                    IsDirectory = true,
                    Children = new System.Collections.ObjectModel.ObservableCollection<FileSystemItem>
                    {
                        new FileSystemItem { Name = "settings.json", Path = "config/settings.json", IsDirectory = false, Size = 256 }
                    }
                }
            };

            // Init mock content
            _fileContents["motions/dance.json"] = "{ \"name\": \"dance\", \"frames\": [] }";
            _fileContents["motions/wave.json"] = "{ \"name\": \"wave\", \"frames\": [] }";
            _fileContents["config/settings.json"] = "{ \"baud\": 115200 }";
        }

        public string? GetFileContent(string path)
        {
            return _fileContents.ContainsKey(path) ? _fileContents[path] : null;
        }

        public void SaveFileContent(string path, string content)
        {
            _fileContents[path] = content;
        }
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
