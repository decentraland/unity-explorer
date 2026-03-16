namespace DCL.CharacterMotion.Components
{
    /// <summary>
    /// Added to the entity when movePlayerTo is invoked.
    /// Removed after teleportation is completed.
    /// Stores info relative the move player to call.
    /// </summary>
    public struct MovePlayerToInfo
    {
        /// <summary>
        /// Indicates when movePlayerTo was invoked.
        /// </summary>
        public long FrameCount;

        public MovePlayerToInfo(long frameCount = 0)
        {
            FrameCount = frameCount;
        }
    }
}
