using CommunicationData.URLHelpers;
using DCL.ECSComponents;
using SceneRunner.Scene;
using Utility.Animations;

namespace DCL.AvatarRendering.Emotes
{
    public struct CharacterMaskedEmoteComponent
    {
        public URN EmoteUrn;
        public bool EmoteLoop;
        public EmoteReferences? CurrentEmoteReference;
        public bool StopEmote;
        public AvatarEmoteMask Mask;
        public bool Paused;
        public ISceneFacade? SourceScene;

        private int currentAnimationTag;

        public readonly bool IsPlaying
        {
            get
            {
                if (Paused) return false;
                if (CurrentEmoteReference == null) return false;
                return currentAnimationTag == AnimationHashes.MASKED_EMOTE || currentAnimationTag == AnimationHashes.MASKED_EMOTE_LOOP;
            }
        }

        public void SetAnimationTag(int tag) => currentAnimationTag = tag;

        public void Reset()
        {
            EmoteUrn = default;
            EmoteLoop = false;
            CurrentEmoteReference = null;
            StopEmote = false;
            Mask = AvatarEmoteMask.AemFullBody;
            Paused = false;
            SourceScene = null;
        }
    }
}
