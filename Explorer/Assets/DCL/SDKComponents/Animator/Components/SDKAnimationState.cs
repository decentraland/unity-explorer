using DCL.ECSComponents;
using DCL.SDKComponents.Tween.Systems;

using UAnimator = UnityEngine.Animator;

namespace DCL.SDKComponents.Animator.Components
{
    public readonly struct SDKAnimationState
    {
        public readonly string Clip;

        /// <summary>
        ///     Mecanim short-name hash of <see cref="Clip" />, precomputed once here so per-frame playback
        ///     observation compares against <c>AnimatorStateInfo.shortNameHash</c> instead of re-hashing the name.
        /// </summary>
        public readonly int ClipHash;

        public readonly bool Playing;
        public readonly float Weight;
        public readonly float Speed;
        public readonly bool Loop;
        public readonly bool ShouldReset;

        /// <summary>
        ///     Edge-trigger latch used by <c>AnimatorFinishWritebackSystem</c>: set once the clip has
        ///     been observed actively playing on a Unity animator, so a later "not active" read means natural
        ///     completion instead of "not started yet". Rebuilding the states from a scene write resets it.
        /// </summary>
        public readonly bool ObservedPlaying;

        public SDKAnimationState(PBAnimationState pbAnimationState)
        {
            Clip = pbAnimationState.Clip;
            ClipHash = UAnimator.StringToHash(pbAnimationState.Clip);
            Playing = pbAnimationState.Playing;
            Weight = pbAnimationState.GetWeight();
            Speed = pbAnimationState.GetSpeed();
            Loop = pbAnimationState.GetLoop();
            ShouldReset = pbAnimationState.GetShouldReset();
            ObservedPlaying = false;
        }

        private SDKAnimationState(string clip, int clipHash, bool playing, float weight, float speed, bool loop, bool shouldReset, bool observedPlaying)
        {
            Clip = clip;
            ClipHash = clipHash;
            Playing = playing;
            Weight = weight;
            Speed = speed;
            Loop = loop;
            ShouldReset = shouldReset;
            ObservedPlaying = observedPlaying;
        }

        public SDKAnimationState WithObserved() =>
            new (Clip, ClipHash, Playing, Weight, Speed, Loop, ShouldReset, observedPlaying: true);

        public SDKAnimationState AsStopped() =>
            new (Clip, ClipHash, playing: false, Weight, Speed, Loop, ShouldReset, observedPlaying: false);
    }
}
