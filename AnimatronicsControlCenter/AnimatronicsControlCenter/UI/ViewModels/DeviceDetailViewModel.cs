using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Models;
using System.Threading.Tasks;

namespace AnimatronicsControlCenter.UI.ViewModels
{
    public partial class DeviceDetailViewModel : ObservableObject
    {
        private readonly ISerialService _serialService;

        [ObservableProperty]
        private Device? selectedDevice;

        public DeviceDetailViewModel(ISerialService serialService)
        {
            _serialService = serialService;
        }

        [RelayCommand]
        private async Task MoveMotorAsync(MotorState motor)
        {
             if (SelectedDevice != null && motor != null)
             {
                 await _serialService.SendCommandAsync(SelectedDevice.Id, "move", new { motorId = motor.Id, pos = motor.Position });
             }
        }
    }
}

