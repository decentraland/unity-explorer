namespace DCL.AvatarRendering.AvatarShape.Components
{
    /// <summary>
    ///     Bridge component written by external services (chat + voice chat) to drive mouth animation.
    ///     <c>AvatarFacialExpressionSystem</c> reads and clears it each frame.
    ///     <para>
    ///         Two independent writers update different fields, so writers must use a partial-update
    ///         pattern (Has + Get or Add) instead of AddOrSet to avoid clobbering each other.
    ///     </para>
    ///     <list type="bullet">
    ///         <item><see cref="PendingMessage"/> / <see cref="MessageIsDirty"/> — set by chat service when a message arrives.</item>
    ///         <item><see cref="IsVoiceChatSpeaking"/> — set by voice handler when speaking state changes.</item>
    ///     </list>
    /// </summary>
    public struct AvatarMouthInputComponent
    {
        public string? PendingMessage;
        public bool MessageIsDirty;
        public bool IsVoiceChatSpeaking;
    }
}