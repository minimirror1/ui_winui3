using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Models;

namespace AnimatronicsControlCenter.Infrastructure
{
    public class SerialService : ISerialService
    {
        private SerialPort? _serialPort;
        private readonly SemaphoreSlim _writeLock = new(1, 1);

        public bool IsConnected => _serialPort?.IsOpen ?? false;

        public async Task ConnectAsync(string portName, int baudRate)
        {
            if (_serialPort != null && _serialPort.IsOpen)
                Disconnect();

            _serialPort = new SerialPort(portName, baudRate)
            {
                ReadTimeout = 500,
                WriteTimeout = 500
            };
            _serialPort.Open();
            await Task.CompletedTask;
        }

        public void Disconnect()
        {
            if (_serialPort?.IsOpen == true)
            {
                _serialPort.Close();
            }
            _serialPort?.Dispose();
            _serialPort = null;
        }

        public async Task SendCommandAsync(int deviceId, string command, object? payload = null)
        {
            if (!IsConnected) return;

            var message = new { id = deviceId, cmd = command, payload };
            var json = JsonSerializer.Serialize(message);
            
            await _writeLock.WaitAsync();
            try
            {
                if (_serialPort?.IsOpen == true)
                {
                    _serialPort.WriteLine(json);
                }
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public async Task<IEnumerable<Device>> ScanDevicesAsync(int startId, int endId)
        {
            var foundDevices = new List<Device>();
            if (!IsConnected) return foundDevices;

            // Run in background to avoid blocking UI if called from UI thread
            return await Task.Run(async () =>
            {
                for (int id = startId; id <= endId; id++)
                {
                    try
                    {
                        await SendCommandAsync(id, "ping");
                        
                        await Task.Delay(50); 
                        
                        if (_serialPort != null && _serialPort.IsOpen && _serialPort.BytesToRead > 0)
                        {
                            var response = _serialPort.ReadLine();
                            if (!string.IsNullOrWhiteSpace(response))
                            {
                                foundDevices.Add(new Device(id) { IsConnected = true, StatusMessage = "Online" });
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Ignore timeouts
                    }
                }
                return foundDevices;
            });
        }
    }
}

