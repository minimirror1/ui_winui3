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
        private readonly Dictionary<string, string> _virtualFileSystem;

        public VirtualDeviceManager()
        {
            _virtualFileSystem = new Dictionary<string, string>
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

                { "Setting/DI_ID.TXT", "DeviceID=1" },
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

        public string ProcessCommand(string jsonCommand)
        {
            try
            {
                var node = JsonNode.Parse(jsonCommand);
                if (node == null) return ErrorResponse("Invalid JSON");

                string cmd = node["cmd"]?.ToString() ?? "";
                var payload = node["payload"];

                return cmd switch
                {
                    "ping" => SuccessResponse(new { message = "pong" }),
                    "move" => HandleMove(payload),
                    "motion_ctrl" => HandleMotionCtrl(payload),
                    "get_files" => HandleGetFiles(),
                    "get_file" => HandleGetFile(payload),
                    "save_file" => HandleSaveFile(payload),
                    "verify_file" => HandleVerifyFile(payload),
                    _ => ErrorResponse($"Unknown command: {cmd}")
                };
            }
            catch (Exception ex)
            {
                return ErrorResponse(ex.Message);
            }
        }

        private string HandleMove(JsonNode? payload)
        {
            // Simulate motor movement
            return SuccessResponse(new { status = "moved" });
        }

        private string HandleMotionCtrl(JsonNode? payload)
        {
            // Simulate motion control
            string action = payload?["action"]?.ToString() ?? "unknown";
            return SuccessResponse(new { status = "executed", action });
        }

        private string HandleGetFiles()
        {
            var rootItems = BuildFileSystemTree();
            return SuccessResponse(rootItems);
        }

        private string HandleGetFile(JsonNode? payload)
        {
            string path = payload?["path"]?.ToString() ?? "";
            if (_virtualFileSystem.TryGetValue(path, out var content))
            {
                return SuccessResponse(new { path, content });
            }
            return ErrorResponse("File not found");
        }

        private string HandleSaveFile(JsonNode? payload)
        {
            string path = payload?["path"]?.ToString() ?? "";
            string content = payload?["content"]?.ToString() ?? "";

            if (string.IsNullOrEmpty(path)) return ErrorResponse("Invalid path");

            // Update or create file
            _virtualFileSystem[path] = content;
            return SuccessResponse(new { status = "saved", path });
        }

        private string HandleVerifyFile(JsonNode? payload)
        {
            string path = payload?["path"]?.ToString() ?? "";
            string contentToCheck = payload?["content"]?.ToString() ?? "";

            if (_virtualFileSystem.TryGetValue(path, out var storedContent))
            {
                // Simple string comparison. In reality, might normalize line endings.
                // For this simulation, we'll strip \r to be safe if mixing environments
                string normalizedStored = storedContent.Replace("\r\n", "\n").Replace("\r", "\n");
                string normalizedCheck = contentToCheck.Replace("\r\n", "\n").Replace("\r", "\n");
                
                return SuccessResponse(new { match = normalizedStored == normalizedCheck });
            }
            
            return ErrorResponse("File not found");
        }

        private List<FileSystemItem> BuildFileSystemTree()
        {
            var rootItems = new List<FileSystemItem>();
            var dirs = new Dictionary<string, FileSystemItem>();

            foreach (var kvp in _virtualFileSystem)
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
                        // Skip placeholder files if desired, but for now keeping them to ensure folder structure
                        // Or we can filter them out from the visual tree if name is placeholder.txt?
                        // Let's keep them for now.
                        
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

        private string SuccessResponse(object payload)
        {
            var response = new
            {
                status = "ok",
                payload
            };
            return JsonSerializer.Serialize(response);
        }

        private string ErrorResponse(string message)
        {
            var response = new
            {
                status = "error",
                message
            };
            return JsonSerializer.Serialize(response);
        }
    }
}
