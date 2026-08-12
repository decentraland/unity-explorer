using DCL.ECSComponents;
using DCL.SDKComponents.Tween.Systems;
using System.Collections.Generic;

using UAnimator = UnityEngine.Animator;

namespace DCL.SDKComponents.Animator.Components
{
    public readonly struct SDKAnimationState
    {
        private static readonly Dictionary<string, (int enabled, int loop, int trigger)> hashCache = new ();

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
        public readonly int EnabledParamHash;
        public readonly int LoopParamHash;
        public readonly int TriggerParamHash;

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

            if (!hashCache.TryGetValue(Clip, out (int enabled, int loop, int trigger) hashes))
            {
                hashes = (UnityEngine.Animator.StringToHash($"{Clip}_Enabled"),
                    UnityEngine.Animator.StringToHash($"{Clip}_Loop"),
                    UnityEngine.Animator.StringToHash($"{Clip}_Trigger"));

                hashCache[Clip] = hashes;
            }

            EnabledParamHash = hashes.enabled;
            LoopParamHash = hashes.loop;
            TriggerParamHash = hashes.trigger;
        }

        private SDKAnimationState(string clip, int clipHash, bool playing, float weight, float speed, bool loop, bool shouldReset, bool observedPlaying, int enabledParamHash, int loopParamHash, int triggerParamHash)
        {
            Clip = clip;
            ClipHash = clipHash;
            Playing = playing;
            Weight = weight;
            Speed = speed;
            Loop = loop;
            ShouldReset = shouldReset;
            ObservedPlaying = observedPlaying;
            EnabledParamHash = enabledParamHash;
            LoopParamHash = loopParamHash;
            TriggerParamHash = triggerParamHash;
        }

        public SDKAnimationState WithObserved() =>
            new (Clip, ClipHash, Playing, Weight, Speed, Loop, ShouldReset, observedPlaying: true, EnabledParamHash, LoopParamHash, TriggerParamHash);

        public SDKAnimationState AsStopped() =>
            new (Clip, ClipHash, playing: false, Weight, Speed, Loop, ShouldReset, observedPlaying: false, EnabledParamHash, LoopParamHash, TriggerParamHash);
    }
}
