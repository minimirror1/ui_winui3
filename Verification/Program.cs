using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using AnimatronicsControlCenter.Infrastructure;
using System.IO;

namespace Verification
{
    class Program
    {
        static void Main(string[] args)
        {
            try 
            {
                // Explicitly set CWD to project root for visibility
                // Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

                var manager = new VirtualDeviceManager();
                string testFile = "Setting/MT_ST.TXT";
                string newValue = "Value=9999";

                // 1. Get initial values
                string dev1Initial = GetFileContent(manager, 1, testFile);
                string dev2Initial = GetFileContent(manager, 2, testFile);
                
                bool pass1 = (dev1Initial == dev2Initial);

                // 2. Modify Device 1
                var saveCmd = new
                {
                    src_id = 0,
                    tar_id = 1,
                    cmd = "save_file",
                    payload = new { path = testFile, content = newValue }
                };
                string saveJson = JsonSerializer.Serialize(saveCmd);
                manager.ProcessCommand(saveJson);

                // 3. Verify Device 1 changed
                string dev1New = GetFileContent(manager, 1, testFile);
                bool pass2 = (dev1New == newValue);

                // 4. Verify Device 2 did NOT change
                string dev2New = GetFileContent(manager, 2, testFile);
                bool pass3 = (dev2New == dev2Initial);

                // Write result to file
                // Use absolute path to ensure we find it
                string path = Path.Combine(Environment.CurrentDirectory, "verification_result.txt");
                string content = $"Initial match: {pass1}\nDev1 Updated: {pass2}\nDev2 Isolated: {pass3}\nDev1New: {dev1New}\nDev2New: {dev2New}";
                
                if (pass1 && pass2 && pass3)
                    content = "VERIFICATION PASSED\n" + content;
                else
                    content = "VERIFICATION FAILED\n" + content;
                    
                File.WriteAllText("verification_result.txt", content);
            }
            catch (Exception ex)
            {
                File.WriteAllText("verification_error.txt", ex.ToString());
            }
        }

        static string GetFileContent(VirtualDeviceManager manager, int deviceId, string path)
        {
            var cmd = new
            {
                src_id = 0,
                tar_id = deviceId,
                cmd = "get_file",
                payload = new { path }
            };
            string json = JsonSerializer.Serialize(cmd);
            string response = manager.ProcessCommand(json);
            
            var node = JsonNode.Parse(response);
            if (node != null && node["status"]?.ToString() == "ok")
            {
                return node["payload"]?["content"]?.ToString() ?? "";
            }
            return "ERROR";
        }
    }
}
