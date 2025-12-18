using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AnimatronicsControlCenter.Core.Models;

namespace AnimatronicsControlCenter.Core.Motors
{
    public static class MotorStateMerger
    {
        public static void Apply(ObservableCollection<MotorState> target, IEnumerable<MotorStatePatch> patches)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (patches == null) throw new ArgumentNullException(nameof(patches));

            foreach (var patch in patches)
            {
                if (patch == null) continue;

                var motor = target.FirstOrDefault(m => m.Id == patch.Id);
                if (motor == null)
                {
                    motor = new MotorState { Id = patch.Id };
                    target.Add(motor);
                }

                ApplyToMotor(motor, patch);
            }
        }

        private static void ApplyToMotor(MotorState motor, MotorStatePatch patch)
        {
            if (patch.GroupId.HasValue) motor.GroupId = patch.GroupId.Value;
            if (patch.SubId.HasValue) motor.SubId = patch.SubId.Value;

            if (patch.Type != null) motor.Type = patch.Type;
            if (patch.Status != null) motor.Status = patch.Status;

            if (patch.Position.HasValue) motor.Position = patch.Position.Value;
            if (patch.Velocity.HasValue) motor.Velocity = patch.Velocity.Value;
        }
    }
}


