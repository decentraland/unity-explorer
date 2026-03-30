namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Readonly snapshot of live reaction simulation stats for debug display.
    /// </summary>
    public readonly struct ChatReactionStats
    {
        public readonly int UIAliveCount;
        public readonly int UIPoolCapacity;
        public readonly int WorldAliveCount;
        public readonly int WorldVisibleCount;
        public readonly int WorldVisibleAnchors;
        public readonly int WorldPoolCapacity;
        public readonly int NearbyAvatarCount;
        public readonly bool IsUIStreaming;
        public readonly bool IsWorldStreaming;
        public readonly bool IsDebugNearbyActive;

        // Anchor table diagnostics
        public readonly int ActiveAnchorCount;
        public readonly int AnchorScanLimit;
        public readonly int AnchorSlotCapacity;

        public ChatReactionStats(
            int uiAlive, int uiCapacity,
            int worldAlive, int worldVisible, int worldVisibleAnchors, int worldCapacity,
            int nearbyAvatars,
            bool uiStreaming, bool worldStreaming, bool debugNearby,
            int activeAnchors, int anchorScanLimit, int anchorSlotCapacity)
        {
            UIAliveCount = uiAlive;
            UIPoolCapacity = uiCapacity;
            WorldAliveCount = worldAlive;
            WorldVisibleCount = worldVisible;
            WorldVisibleAnchors = worldVisibleAnchors;
            WorldPoolCapacity = worldCapacity;
            NearbyAvatarCount = nearbyAvatars;
            IsUIStreaming = uiStreaming;
            IsWorldStreaming = worldStreaming;
            IsDebugNearbyActive = debugNearby;
            ActiveAnchorCount = activeAnchors;
            AnchorScanLimit = anchorScanLimit;
            AnchorSlotCapacity = anchorSlotCapacity;
        }
    }
}
