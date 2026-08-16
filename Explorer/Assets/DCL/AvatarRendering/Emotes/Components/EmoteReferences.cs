using DCL.AvatarRendering.Loading.Assets;
using UnityEngine;

// ReSharper disable InconsistentNaming

namespace DCL.AvatarRendering.Emotes
{
    public class EmoteReferences : MonoBehaviour
    {
        public int propClipHash { get; private set; }
        public AnimationClip? avatarClip { get; private set; }
        public AnimationClip? propClip { get; private set; }
        public Animator? animatorComp { get; private set; }
        public Animation? animationComp { get; private set; }
        public bool legacy { get; private set; }

        public AudioSource? audioSource;

        /// <summary>
        ///     Pins the source attachment while this instance plays: the instance shares meshes, materials and
        ///     clips with the source, and the storage disposes assets whose ReferenceCount reaches 0.
        ///     Set on acquire, dereferenced and cleared on pool release.
        /// </summary>
        public AttachmentRegularAsset? sourceAsset;

        public void Initialize(AnimationClip? animationClip, AnimationClip? propAnimationClip, Animator? animator, Animation? animation, int propAnimationClipHash, bool legacy)
        {
            avatarClip = animationClip;
            propClip = propAnimationClip;
            animatorComp = animator;
            animationComp = animation;
            propClipHash = propAnimationClipHash;
            this.legacy = legacy;
        }
    }
}
