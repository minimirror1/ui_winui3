using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace AnimatronicsControlCenter.Core.Models
{
    public partial class Device : ObservableObject
    {
        [ObservableProperty]
        private int id;

        [ObservableProperty]
        private bool isConnected;

        [ObservableProperty]
        private string statusMessage;

        public ObservableCollection<MotorState> Motors { get; } = new();

        public Device(int id)
        {
            Id = id;
            IsConnected = false;
            StatusMessage = "Unknown";
            
            // Mock motors for testing
            Motors.Add(new MotorState { Id = 1, Position = 90 });
            Motors.Add(new MotorState { Id = 2, Position = 45 });
        }
    }
}

