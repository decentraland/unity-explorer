﻿using DCL.CharacterMotion.Animation;
using UnityEngine;

namespace DCL.CharacterMotion.Components
{
    public struct CharacterAnimationComponent
    {
        // buffer values for animation states
        public struct AnimationStates
        {
            public float MovementBlendValue;
            public float SlideBlendValue;
            public bool IsGrounded;
            public bool IsJumping;
            public bool IsLongJump;
            public bool IsLongFall;
            public bool IsFalling;
        }

        public AnimationStates States;
    }
}
