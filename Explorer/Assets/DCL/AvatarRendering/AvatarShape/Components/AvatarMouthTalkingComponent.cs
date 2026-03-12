namespace DCL.AvatarRendering.AvatarShape.Components
{
    /// <summary>
    ///     Bridge component set by NametagPlacementSystem when a chat bubble arrives for an avatar.
    ///     AvatarFacialAnimationSystem reads this to drive phoneme animation without requiring
    ///     a direct assembly dependency on DCL.Chat.
    /// </summary>
    public struct AvatarMouthTalkingComponent
    {
        /// <summary>
        ///     The chat message text to animate through phonemes.
        /// </summary>
        public string Message;

        /// <summary>
        ///     True when a new, unconsumed message has arrived. Reset to false by AvatarFacialAnimationSystem.
        /// </summary>
        public bool IsDirty;
    }
}
