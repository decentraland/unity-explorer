using DCL.ECSComponents;
using DCL.SDKComponents.Tween.Systems;
using System.Collections.Generic;

namespace DCL.SDKComponents.Animator.Components
{
    public readonly struct SDKAnimationState
    {
        private static readonly Dictionary<string, (int enabled, int loop, int trigger)> hashCache = new ();

        public readonly string Clip;
        public readonly bool Playing;
        public readonly float Weight;
        public readonly float Speed;
        public readonly bool Loop;
        public readonly bool ShouldReset;

        public readonly int EnabledParamHash;
        public readonly int LoopParamHash;
        public readonly int TriggerParamHash;

        public SDKAnimationState(PBAnimationState pbAnimationState)
        {
            Clip = pbAnimationState.Clip;
            Playing = pbAnimationState.Playing;
            Weight = pbAnimationState.GetWeight();
            Speed = pbAnimationState.GetSpeed();
            Loop = pbAnimationState.GetLoop();
            ShouldReset = pbAnimationState.GetShouldReset();

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
    }
}
