namespace DCL.Chat.ChatReactions.Debug
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
        public readonly int DroppedThisFrame;
        public readonly int CappedThisFrame;
        public readonly int LocalAnchorAlive;
        public readonly bool IsUIStreaming;
        public readonly bool IsWorldStreaming;
        public readonly bool IsDebugNearbyActive;

        // Dynamic scaling
        public readonly int EffectiveMaxPerAvatar;

        // Anchor table diagnostics
        public readonly int ActiveAnchorCount;
        public readonly int AnchorScanLimit;
        public readonly int AnchorSlotCapacity;

        public ChatReactionStats(
            int uiAlive, int uiCapacity,
            int worldAlive, int worldVisible, int worldVisibleAnchors, int worldCapacity,
            int nearbyAvatars,
            int dropped, int capped, int localAnchorAlive,
            bool uiStreaming, bool worldStreaming, bool debugNearby,
            int effectiveMaxPerAvatar,
            int activeAnchors, int anchorScanLimit, int anchorSlotCapacity)
        {
            UIAliveCount = uiAlive;
            UIPoolCapacity = uiCapacity;
            WorldAliveCount = worldAlive;
            WorldVisibleCount = worldVisible;
            WorldVisibleAnchors = worldVisibleAnchors;
            WorldPoolCapacity = worldCapacity;
            NearbyAvatarCount = nearbyAvatars;
            DroppedThisFrame = dropped;
            CappedThisFrame = capped;
            LocalAnchorAlive = localAnchorAlive;
            IsUIStreaming = uiStreaming;
            IsWorldStreaming = worldStreaming;
            IsDebugNearbyActive = debugNearby;
            EffectiveMaxPerAvatar = effectiveMaxPerAvatar;
            ActiveAnchorCount = activeAnchors;
            AnchorScanLimit = anchorScanLimit;
            AnchorSlotCapacity = anchorSlotCapacity;
        }
    }
}
