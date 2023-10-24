using DCL.CharacterMotion.Animation;
using UnityEngine;

namespace DCL.CharacterMotion.Components
{
    public class CharacterAnimationComponent
    {
        public struct AnimationStates
        {
            public float MovementBlendValue;
            public bool IsGrounded;
            public bool IsJumping;
            public bool IsLongJump;
            public bool IsLongFall;
            public bool IsFalling;
            public bool IsStunned;
        }

        public class AnimationTrigger
        {
            private readonly int animationParameter;
            private bool trigger;

            public AnimationTrigger(int animationParameter)
            {
                this.animationParameter = animationParameter;
            }

            public void Execute()
            {
                trigger = true;
            }

            public void Trigger(Animator animator)
            {
                if (!trigger) return;
                trigger = false;
                animator.SetTrigger(animationParameter);
            }
        }

        public class AnimationTriggers
        {
            public readonly AnimationTrigger Jump = new (AnimationHashes.JUMP);
        }

        public AnimationStates States;
        public readonly AnimationTriggers Triggers;

        public CharacterAnimationComponent(AnimationTriggers triggers)
        {
            Triggers = triggers;
        }
    }
}
