using UnityEngine;

namespace DCL.AvatarRendering.Emotes
{
    public class EmoteReferences : MonoBehaviour
    {
        public struct EmoteOutcome
        {
            public AnimationClip? LocalAvatarAnimation;
            public AnimationClip? OtherAvatarAnimation;
            public AnimationClip? PropAnimation;
            public int PropAnimationHash;
        }

        public int propClipHash { get; private set; }
        public AnimationClip? avatarClip { get; private set; }
        public AnimationClip? propClip { get; private set; }
        public Animator? animatorComp { get; private set; }
        public Animation? animationComp { get; private set; }
        public bool legacy { get; private set; }
        public EmoteOutcome[]? socialEmoteOutcomes { get; private set; }

        public AudioSource? audioSource;

        public void Initialize(AnimationClip? animationClip, AnimationClip? propCLip, EmoteOutcome[]? socialEmoteOutcomes, Animator? animatorComp, Animation? animationComp, int propClipHash, bool legacy)
        {
            this.avatarClip = animationClip;
            this.propClip = propCLip;
            this.animatorComp = animatorComp;
            this.animationComp = animationComp;
            this.propClipHash = propClipHash;
            this.legacy = legacy;
            this.socialEmoteOutcomes = socialEmoteOutcomes;
        }
    }
}
