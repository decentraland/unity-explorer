using DCL.ECSComponents;
using DCL.SDKComponents.Animator.Extensions;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SDKComponents.Animator.Components
{
    public readonly struct LoadedAnimation
    {
        private readonly Animation animation;
        private readonly Couple couple;

        public LoadedAnimation(Animation animation)
        {
            this.animation = animation;
            couple = new Couple();
        }

        public string? PlayingClip() =>
            couple.PlayingAnimation?.Clip;

        public void Initialize()
        {
            animation.Initialize();
        }

        public void Update()
        {
            if (this.couple is { HasPlayed: false, PlayingAnimation: { } })
            {
                animation.TryPlay(this.couple.PlayingAnimation.Clip!);
                couple.HasPlayed = true;
            }

            if (this.couple.StoppedAnimation != null)
                animation.TryStop(this.couple.StoppedAnimation.Clip!);
        }

        public void Apply(PBAnimationState? playingAnimation, PBAnimationState? stoppedAnimation)
        {
            this.couple.PlayingAnimation = playingAnimation;
            this.couple.StoppedAnimation = stoppedAnimation;

            if (playingAnimation != null)
            {
                animation.ApplySettings(playingAnimation);
                couple.HasPlayed = false;
            }

            if (stoppedAnimation != null)
                animation.ApplySettings(stoppedAnimation);
        }

        public static (PBAnimationState? playingAnimation, PBAnimationState? stoppedAnimation) RequiredAnimations(IEnumerable<PBAnimationState> animationStates)
        {
            PBAnimationState? playingAnimation = null;
            PBAnimationState? stoppedAnimation = null;

            foreach (PBAnimationState pbAnimationState in animationStates)
                if (pbAnimationState.Playing)
                    playingAnimation = pbAnimationState;
                else if (pbAnimationState.ShouldReset)
                    stoppedAnimation = pbAnimationState;

            return (playingAnimation, stoppedAnimation);
        }

        private class Couple
        {
            public PBAnimationState? PlayingAnimation;
            public PBAnimationState? StoppedAnimation;

            public bool HasPlayed;
        }
    }
}
