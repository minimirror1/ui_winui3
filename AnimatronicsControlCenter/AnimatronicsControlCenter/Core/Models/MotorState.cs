using CommunityToolkit.Mvvm.ComponentModel;

namespace AnimatronicsControlCenter.Core.Models
{
    public partial class MotorState : ObservableObject
    {
        [ObservableProperty]
        private int id;

        [ObservableProperty]
        private int groupId;

        [ObservableProperty]
        private int subId;

        [ObservableProperty]
        private string type = "Servo"; // Default type

        [ObservableProperty]
        private string status = "Normal"; // Default status

        [ObservableProperty]
        private double position;

        [ObservableProperty]
        private double velocity;

        public string DisplayId => $"{GroupId}-{SubId}";
    }
}
