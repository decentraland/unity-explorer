using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace DCL.AvatarRendering.Emotes
{
    public class EmoteReferences : MonoBehaviour
    {
        public int propClipHash { get; private set; }
        public AnimationClip? avatarClip { get; private set; }
        public AnimationClip? propClip { get; private set; }
        public Animator? animatorComp { get; private set; }

        public AudioSource? audioSource;

        // Playable-graph fork (scene emote in local-scene-dev player builds).
        // All default-initialised => not valid / null. EmotePlayer's pool release callback tears them down.
        public PlayableGraph playableGraph;
        public AnimatorControllerPlayable playableController;
        public AnimationClipPlayable playableClip;
        public Animator? playableSourceAnimator;
        public bool playableLoop;
        public float playableClipLength;

        public void Initialize(AnimationClip? animationClip, AnimationClip? propClip, Animator? animatorComp, int propClipHash)
        {
            this.avatarClip = animationClip;
            this.propClip = propClip;
            this.animatorComp = animatorComp;
            this.propClipHash = propClipHash;
        }

        private void LateUpdate()
        {
            if (!playableGraph.IsValid()) return;

            // Parameter sync: the AnimatorControllerPlayable created in PlayMaskedPlayableEmote is a
            // fresh instance with default state (Speed=0 etc.). The avatar's real Animator still
            // updates its own parameter dictionary every frame (locomotion system etc.); mirror those
            // values into the Playable so layer 0 drives the walk/run clips as expected.
            if (playableController.IsValid() && playableSourceAnimator != null)
            {
                AnimatorControllerParameter[] parameters = playableSourceAnimator.parameters;
                for (int i = 0; i < parameters.Length; i++)
                {
                    AnimatorControllerParameter p = parameters[i];
                    int hash = p.nameHash;
                    switch (p.type)
                    {
                        case AnimatorControllerParameterType.Float:
                            playableController.SetFloat(hash, playableSourceAnimator.GetFloat(hash));
                            break;
                        case AnimatorControllerParameterType.Int:
                            playableController.SetInteger(hash, playableSourceAnimator.GetInteger(hash));
                            break;
                        case AnimatorControllerParameterType.Bool:
                            playableController.SetBool(hash, playableSourceAnimator.GetBool(hash));
                            break;
                        // Triggers intentionally not synced — consumed-on-use, no clean mirror semantic.
                    }
                }
            }

            // Manual loop: AnimationClipPlayable does not honour wrapMode, so we wrap time ourselves.
            if (playableLoop && playableClip.IsValid() && playableClipLength > 0f)
            {
                double t = playableClip.GetTime();
                if (t >= playableClipLength)
                    playableClip.SetTime(t % playableClipLength);
            }
        }
    }
}
