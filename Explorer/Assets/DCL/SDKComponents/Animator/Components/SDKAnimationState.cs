using DCL.ECSComponents;
using DCL.SDKComponents.Tween.Systems;
using UnityEngine;

namespace DCL.SDKComponents.Animator.Components
{
    public readonly struct SDKAnimationState
    {
        public readonly string Clip;
        public readonly bool Playing;
        public readonly bool ShouldReset;
        private readonly float weight;
        private readonly float speed;
        private readonly bool loop;

        public SDKAnimationState(PBAnimationState pbAnimationState)
        {
            Clip = pbAnimationState.Clip!;
            Playing = pbAnimationState.Playing;
            weight = pbAnimationState.GetWeight();
            speed = pbAnimationState.GetSpeed();
            loop = pbAnimationState.GetLoop();
            ShouldReset = pbAnimationState.GetShouldReset();
        }

        public void ApplyOn(AnimationState animationState)
        {
            animationState.weight = weight;

            animationState.wrapMode = loop ? WrapMode.Loop : WrapMode.Default;

            animationState.clip!.wrapMode = animationState.wrapMode;
            animationState.speed = speed;
            animationState.enabled = Playing;
        }
    }
}
