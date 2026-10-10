namespace DCL.AvatarRendering.Emotes
{
    /// <summary>
    /// Added to an entity that wants to play a <b>SCENE</b> emote for the time the emote is still being loaded.
    /// The component purpose is to let other systems know that the scene is trying to play an emote, but that loading
    /// of the emote isn't completed yet.
    /// </summary>
    /// <seealso cref="GetSceneEmoteFromLocalSceneIntention"/>
    /// <seealso cref="GetSceneEmoteFromRealmIntention"/>
    public struct CharacterWaitingSceneEmoteLoading
    {
        /// <summary>
        /// Indicates when the loading started.
        /// </summary>
        public long FrameCount;

        public CharacterWaitingSceneEmoteLoading(long frameCount = 0)
        {
            FrameCount = frameCount;
        }
    }
}
