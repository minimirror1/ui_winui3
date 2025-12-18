namespace AnimatronicsControlCenter.Core.Motors
{
    /// <summary>
    /// Partial update payload for a motor. Only non-null fields should be applied.
    /// </summary>
    public sealed class MotorStatePatch
    {
        public int Id { get; init; }

        public int? GroupId { get; init; }
        public int? SubId { get; init; }
        public string? Type { get; init; }
        public string? Status { get; init; }

        public double? Position { get; init; }
        public double? Velocity { get; init; }
    }
}


