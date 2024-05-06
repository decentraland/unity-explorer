using UnityEngine;

namespace DCL.AvatarRendering.Emotes
{
    public class EmoteReferences : MonoBehaviour
    {
        public int propClipHash { get; private set; }
        public AnimationClip? avatarClip { get; private set; }
        public AnimationClip? propClip { get; private set; }
        public Animator animator { get; private set; }

        public AudioSource? audioSource;

        public void Initialize(AnimationClip? animationClip, AnimationClip? propCLip, Animator animator, int propClipHash)
        {
            this.avatarClip = animationClip;
            this.propClip = propCLip;
            this.animator = animator;
            this.propClipHash = propClipHash;
        }
    }
}
