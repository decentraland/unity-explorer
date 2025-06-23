using CommunicationData.URLHelpers;
using Utility.Animations;

namespace DCL.AvatarRendering.Emotes
{
    public struct CharacterEmoteComponent
    {
        public URN EmoteUrn;
        public bool EmoteLoop;
        public EmoteReferences? CurrentEmoteReference;
        public int CurrentAnimationTag;
        public bool StopEmote;

        public void Reset()
        {
            EmoteLoop = false;
            CurrentEmoteReference = null;
            StopEmote = false;
        }

        /// <summary>
        ///     Whether the avatar is currently playing an emote.
        /// </summary>
        public readonly bool IsPlayingEmote()
        {
            return CurrentAnimationTag == AnimationHashes.EMOTE || CurrentAnimationTag == AnimationHashes.EMOTE_LOOP;
        }
    }
}
