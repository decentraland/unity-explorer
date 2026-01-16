namespace DCL.Analytics.Systems
{
    /// <summary>
    ///     To hold analytics data across systems
    /// </summary>
    public struct AvatarAnalytics
    {
        public readonly float StartedAt;
        public readonly int WearablesCount;

        public float WearablesResolvedAt;
        public float MissingPointersCounter;

        public AvatarAnalytics(float startedAt, int wearablesCount) : this()
        {
            StartedAt = startedAt;
            WearablesCount = wearablesCount;

            // Non-resolved indicator
            WearablesResolvedAt = -1;
        }
    }
}
