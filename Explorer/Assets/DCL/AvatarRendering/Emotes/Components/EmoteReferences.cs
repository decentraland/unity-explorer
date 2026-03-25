using UnityEngine;

namespace DCL.AvatarRendering.Emotes
{
    public class EmoteReferences : MonoBehaviour
    {
        public int propClipHash { get; private set; }
        public AnimationClip? avatarClip { get; private set; }
        public AnimationClip? propClip { get; private set; }
        public Animator? animatorComp { get; private set; }

        public AudioSource? audioSource;

        public void Initialize(AnimationClip? animationClip, AnimationClip? propClip, Animator? animatorComp, int propClipHash)
        {
            this.avatarClip = animationClip;
            this.propClip = propClip;
            this.animatorComp = animatorComp;
            this.propClipHash = propClipHash;
        }
    }
}
