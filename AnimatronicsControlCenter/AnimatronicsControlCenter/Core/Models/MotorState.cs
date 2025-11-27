using CommunityToolkit.Mvvm.ComponentModel;

namespace AnimatronicsControlCenter.Core.Models
{
    public partial class MotorState : ObservableObject
    {
        [ObservableProperty]
        private int id;

        [ObservableProperty]
        private double position;

        [ObservableProperty]
        private double velocity;
    }
}

