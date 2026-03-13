namespace DCL.AvatarRendering.AvatarShape.Components
{
    /// <summary>
    ///     Bridge component set by VoiceChatNametagsHandler when an avatar's voice-chat speaking
    ///     state changes. <c>AvatarFacialAnimationSystem</c> reads this to drive a looping phoneme
    ///     animation while the avatar is actively talking.
    ///     Placed in the AvatarShape assembly so VoiceChat does not need to depend on it directly.
    /// </summary>
    public struct AvatarVoiceChatMouthComponent
    {
        /// <summary>
        ///     True while this avatar is an active speaker in the current voice-chat session.
        /// </summary>
        public bool IsSpeaking;
    }
}
