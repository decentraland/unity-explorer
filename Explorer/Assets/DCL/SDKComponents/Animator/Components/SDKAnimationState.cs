using DCL.ECSComponents;
using DCL.SDKComponents.Tween.Systems;

namespace DCL.SDKComponents.Animator.Components
{
    public readonly struct SDKAnimationState
    {
        public readonly string Clip;
        public readonly bool Playing;
        public readonly float Weight;
        public readonly float Speed;
        public readonly bool Loop;
        public readonly bool ShouldReset;

        /// <summary>
        ///     Edge-trigger latch used by <see cref="Systems.AnimatorFinishWritebackSystem" />: set once the clip has
        ///     been observed actively playing on a Unity animator, so a later "not active" read means natural
        ///     completion instead of "not started yet". Rebuilding the states from a scene write resets it.
        /// </summary>
        public readonly bool ObservedPlaying;

        public SDKAnimationState(PBAnimationState pbAnimationState)
        {
            Clip = pbAnimationState.Clip;
            Playing = pbAnimationState.Playing;
            Weight = pbAnimationState.GetWeight();
            Speed = pbAnimationState.GetSpeed();
            Loop = pbAnimationState.GetLoop();
            ShouldReset = pbAnimationState.GetShouldReset();
            ObservedPlaying = false;
        }

        private SDKAnimationState(string clip, bool playing, float weight, float speed, bool loop, bool shouldReset, bool observedPlaying)
        {
            Clip = clip;
            Playing = playing;
            Weight = weight;
            Speed = speed;
            Loop = loop;
            ShouldReset = shouldReset;
            ObservedPlaying = observedPlaying;
        }

        public SDKAnimationState WithObserved() =>
            new (Clip, Playing, Weight, Speed, Loop, ShouldReset, observedPlaying: true);

        public SDKAnimationState AsStopped() =>
            new (Clip, playing: false, Weight, Speed, Loop, ShouldReset, observedPlaying: false);
    }
}
