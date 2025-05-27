using UnityEngine;

namespace DCL.AvatarRendering.Emotes
{
    public class EmoteReferences : MonoBehaviour
    {
        public int propClipHash { get; private set; }
        public AnimationClip? avatarClip { get; private set; }
        public AnimationClip? propClip { get; private set; }
        public Animator? animatorComp { get; private set; }
        public Animation? animationComp { get; private set; }

        public AudioSource? audioSource;

        public void Initialize(AnimationClip? animationClip, AnimationClip? propCLip, Animator? animatorComp, Animation? animationComp, int propClipHash)
        {
            this.avatarClip = animationClip;
            this.propClip = propCLip;
            this.animatorComp = animatorComp;
            this.animationComp = animationComp;
            this.propClipHash = propClipHash;
        }
    }
}
