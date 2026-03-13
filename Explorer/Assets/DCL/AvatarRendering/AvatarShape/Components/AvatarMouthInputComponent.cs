namespace DCL.AvatarRendering.AvatarShape.Components
{
    /// <summary>
    ///     Bridge component written by external systems to drive mouth animation on an avatar.
    ///     <c>AvatarFacialAnimationSystem</c> reads and clears this each frame.
    ///     <para>
    ///         Two independent writers update different fields, so writers must use a
    ///         partial-update pattern (Has + Get or Add) instead of AddOrSet to avoid
    ///         clobbering each other's state.
    ///     </para>
    ///     <list type="bullet">
    ///         <item><see cref="PendingMessage"/> / <see cref="MessageIsDirty"/> — set by <c>NametagPlacementSystem</c> when a chat bubble arrives.</item>
    ///         <item><see cref="IsVoiceChatSpeaking"/> — set by <c>VoiceChatMouthAnimationHandler</c> when speaking state changes.</item>
    ///     </list>
    /// </summary>
    public struct AvatarMouthInputComponent
    {
        /// <summary>
        ///     The chat message text to animate through phonemes. Null when no message is pending.
        /// </summary>
        public string? PendingMessage;

        /// <summary>
        ///     True when <see cref="PendingMessage"/> has not been consumed yet.
        ///     Reset to false by <c>AvatarFacialAnimationSystem</c> after reading the message.
        /// </summary>
        public bool MessageIsDirty;

        /// <summary>
        ///     True while this avatar is an active speaker in the current voice-chat session.
        ///     When true, <c>AvatarFacialAnimationSystem</c> loops a hardcoded phoneme string.
        /// </summary>
        public bool IsVoiceChatSpeaking;
    }
}
