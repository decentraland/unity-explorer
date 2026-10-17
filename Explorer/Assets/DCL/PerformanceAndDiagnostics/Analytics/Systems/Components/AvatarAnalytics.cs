namespace DCL.Analytics.Systems
{
    /// <summary>
    ///     To hold analytics data across systems
    /// </summary>
    public struct AvatarAnalytics
    {
        public const int WEARABLES_NOT_RESOLVED = -1;

        public readonly float StartedAt;
        public readonly int WearablesCount;

        public float WearablesResolvedAt;
        public float MissingPointersCounter;

        /// <summary>
        ///     The number of wearables for which assets are needed and resolved
        /// </summary>
        public int VisibleWearablesCount;

        public AvatarAnalytics(float startedAt, int wearablesCount) : this()
        {
            StartedAt = startedAt;
            WearablesCount = wearablesCount;

            // Non-resolved indicator
            WearablesResolvedAt = WEARABLES_NOT_RESOLVED;
        }
    }
}
