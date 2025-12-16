using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using AnimatronicsControlCenter.Core.Models;

namespace AnimatronicsControlCenter.Infrastructure
{
    public class VirtualDeviceManager
    {
        private const byte BroadcastId = 255;

        // Store file systems per device ID
        private readonly Dictionary<int, Dictionary<string, string>> _deviceFileSystems;
        private readonly object _lock = new object();

        public VirtualDeviceManager()
        {
            _deviceFileSystems = new Dictionary<int, Dictionary<string, string>>();
        }

        private Dictionary<string, string> GetDefaultFileSystem(int deviceId)
        {
            return new Dictionary<string, string>
            {
                { "Error/err_lv.ini", "[ErrorLevel]\nLevel=1" },
                { "Error/ERR_LVF.TXT", "2023-10-27 10:00:00 ERROR_01" },
                { "Error/note.ini", "Note=Check sensors" },

                { "Log/BOOT.TXT", "Boot Log..." },
                { "Log/ERROR.TXT", "Error Log..." },
                { "Log/INSP.TXT", "Inspection Log..." },
                { "Log/SENSOR.TXT", "Sensor Log..." },

                { "Media/MT_2.CSV", "Time,Pos\n0,10\n100,20" },
                { "Media/MT_3.CSV", "Time,Pos\n0,10\n100,20" },
                { "Media/MT_4.CSV", "Time,Pos\n0,10\n100,20" },
                { "Media/MT_5.CSV", "Time,Pos\n0,10\n100,20" },
                { "Media/MT_6.CSV", "Time,Pos\n0,10\n100,20" },
                { "Media/MT_ALL.CSV", "Time,Pos\n0,10\n100,20" },

                { "Midi/motor/placeholder.txt", "" }, // Placeholder for empty folder structure
                { "Midi/page/placeholder.txt", "" },  // Placeholder for empty folder structure

                { "Setting/DI_ID.TXT", $"DeviceID={deviceId}" },
                { "Setting/MT_AT.TXT", "Value=100" },
                { "Setting/MT_ATT.TXT", "Value=200" },
                { "Setting/MT_CT.TXT", "Value=300" },
                { "Setting/MT_EL.TXT", "Value=400" },
                { "Setting/MT_LI.TXT", "Value=500" },
                { "Setting/MT_LK.TXT", "Value=600" },
                { "Setting/MT_MD.TXT", "Value=700" },
                { "Setting/MT_MS.TXT", "Value=800" },
                { "Setting/MT_PL.TXT", "Value=900" },
                { "Setting/MT_RP.TXT", "Value=1000" },
                { "Setting/MT_ST.TXT", "Value=1100" },
                { "Setting/RE_TI.TXT", "Value=1200" }
            };
        }

        private Dictionary<string, string> GetFileSystem(int deviceId)
        {
            if (!_deviceFileSystems.ContainsKey(deviceId))
            {
                _deviceFileSystems[deviceId] = GetDefaultFileSystem(deviceId);
            }
            return _deviceFileSystems[deviceId];
        }

        public string ProcessCommand(string jsonCommand)
        {
            lock (_lock)
            {
                try
                {
                    var node = JsonNode.Parse(jsonCommand);
                    if (node == null) return ErrorResponse(0, 0, "error", "Invalid JSON");

                    // Firmware-style addressing: { src_id, tar_id, cmd, payload }
                    // Keep a backward-compatibility fallback to legacy { id, ... } to avoid breaking older tools.
                    int srcId = node["src_id"]?.GetValue<int>() ?? 0;
                    int tarId = node["tar_id"]?.GetValue<int>() ?? (node["id"]?.GetValue<int>() ?? 0);

                    // Broadcast means "do not respond" (mirrors firmware collision-avoidance behavior).
                    if (tarId == BroadcastId) return string.Empty;

                    int deviceId = tarId;
                    string cmd = node["cmd"]?.ToString() ?? "";
                    var payload = node["payload"];

                    return cmd switch
                    {
                        "ping" => SuccessResponse(deviceId, srcId, "pong", new { message = "pong" }),
                        "move" => HandleMove(deviceId, srcId, payload),
                        "motion_ctrl" => HandleMotionCtrl(deviceId, srcId, payload),
                        "get_files" => HandleGetFiles(deviceId, srcId),
                        "get_file" => HandleGetFile(deviceId, srcId, payload),
                        "save_file" => HandleSaveFile(deviceId, srcId, payload),
                        "verify_file" => HandleVerifyFile(deviceId, srcId, payload),
                        _ => ErrorResponse(deviceId, srcId, "error", $"Unknown command: {cmd}")
                    };
                }
                catch (Exception ex)
                {
                    return ErrorResponse(0, 0, "error", ex.Message);
                }
            }
        }

        private string HandleMove(int deviceId, int srcId, JsonNode? payload)
        {
            // Simulate motor movement
            return SuccessResponse(deviceId, srcId, "move", new { status = "moved", deviceId });
        }

        private string HandleMotionCtrl(int deviceId, int srcId, JsonNode? payload)
        {
            // Simulate motion control
            string action = payload?["action"]?.ToString() ?? "unknown";
            return SuccessResponse(deviceId, srcId, "motion_ctrl", new { status = "executed", action, deviceId });
        }

        private string HandleGetFiles(int deviceId, int srcId)
        {
            var fileSystem = GetFileSystem(deviceId);
            var rootItems = BuildFileSystemTree(fileSystem);
            return SuccessResponse(deviceId, srcId, "get_files", rootItems);
        }

        private string HandleGetFile(int deviceId, int srcId, JsonNode? payload)
        {
            string path = payload?["path"]?.ToString() ?? "";
            var fileSystem = GetFileSystem(deviceId);

            if (fileSystem.TryGetValue(path, out var content))
            {
                return SuccessResponse(deviceId, srcId, "get_file", new { path, content });
            }
            return ErrorResponse(deviceId, srcId, "error", "File not found");
        }

        private string HandleSaveFile(int deviceId, int srcId, JsonNode? payload)
        {
            string path = payload?["path"]?.ToString() ?? "";
            string content = payload?["content"]?.ToString() ?? "";

            if (string.IsNullOrEmpty(path)) return ErrorResponse(deviceId, srcId, "error", "Invalid path");

            var fileSystem = GetFileSystem(deviceId);
            
            // Update or create file
            fileSystem[path] = content;
            return SuccessResponse(deviceId, srcId, "save_file", new { status = "saved", path });
        }

        private string HandleVerifyFile(int deviceId, int srcId, JsonNode? payload)
        {
            string path = payload?["path"]?.ToString() ?? "";
            string contentToCheck = payload?["content"]?.ToString() ?? "";

            var fileSystem = GetFileSystem(deviceId);

            if (fileSystem.TryGetValue(path, out var storedContent))
            {
                // Simple string comparison. In reality, might normalize line endings.
                // For this simulation, we'll strip \r to be safe if mixing environments
                string normalizedStored = storedContent.Replace("\r\n", "\n").Replace("\r", "\n");
                string normalizedCheck = contentToCheck.Replace("\r\n", "\n").Replace("\r", "\n");
                
                return SuccessResponse(deviceId, srcId, "verify_file", new { match = normalizedStored == normalizedCheck });
            }
            
            return ErrorResponse(deviceId, srcId, "error", "File not found");
        }

        private List<FileSystemItem> BuildFileSystemTree(Dictionary<string, string> fileSystem)
        {
            var rootItems = new List<FileSystemItem>();
            var dirs = new Dictionary<string, FileSystemItem>();

            foreach (var kvp in fileSystem)
            {
                string fullPath = kvp.Key;
                long size = kvp.Value.Length;
                
                string[] parts = fullPath.Split('/');
                string currentPath = "";
                
                FileSystemItem? parentDir = null;

                for (int i = 0; i < parts.Length; i++)
                {
                    string part = parts[i];
                    bool isFile = (i == parts.Length - 1);
                    currentPath = string.IsNullOrEmpty(currentPath) ? part : currentPath + "/" + part;

                    if (isFile)
                    {
                        var fileItem = new FileSystemItem
                        {
                            Name = part,
                            Path = fullPath, // Use the full relative path as ID
                            IsDirectory = false,
                            Size = size
                        };

                        if (parentDir != null)
                        {
                            if (!parentDir.Children.Any(c => c.Name == fileItem.Name))
                                parentDir.Children.Add(fileItem);
                        }
                        else
                        {
                            if (!rootItems.Any(r => r.Name == fileItem.Name))
                                rootItems.Add(fileItem);
                        }
                    }
                    else
                    {
                        // It's a directory
                        if (!dirs.ContainsKey(currentPath))
                        {
                            var newDir = new FileSystemItem
                            {
                                Name = part,
                                Path = currentPath,
                                IsDirectory = true,
                                Size = 0 // Folder size calculation skipped for simplicity
                            };
                            dirs[currentPath] = newDir;

                            if (parentDir != null)
                            {
                                if (!parentDir.Children.Any(c => c.Name == newDir.Name))
                                    parentDir.Children.Add(newDir);
                            }
                            else
                            {
                                if (!rootItems.Any(r => r.Name == newDir.Name))
                                    rootItems.Add(newDir);
                            }
                        }
                        parentDir = dirs[currentPath];
                    }
                }
            }

            return rootItems;
        }

        private string SuccessResponse(int deviceId, int requestSrcId, string cmd, object payload)
        {
            var response = new
            {
                src_id = deviceId,
                tar_id = requestSrcId,
                cmd,
                status = "ok",
                payload
            };
            return JsonSerializer.Serialize(response);
        }

        private string ErrorResponse(int deviceId, int requestSrcId, string cmd, string message)
        {
            var response = new
            {
                src_id = deviceId,
                tar_id = requestSrcId,
                cmd,
                status = "error",
                message
            };
            return JsonSerializer.Serialize(response);
        }
    }
}
