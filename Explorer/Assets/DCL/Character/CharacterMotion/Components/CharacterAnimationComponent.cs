using DCL.Character.CharacterMotion.Emotes;
using System;
using UnityEngine;

namespace DCL.CharacterMotion.Components
{
    public struct CharacterAnimationComponent
    {
        // buffer values for animation states
        [Serializable]
        public struct AnimationStates
        {
            public float MovementBlendValue;
            public float SlideBlendValue;
            public bool IsGrounded;
            public bool IsJumping;
            public bool IsLongJump;
            public bool IsLongFall;
            public bool IsFalling;
            public bool WasEmoteJustTriggered;
            public bool EmoteLoop;
            public AnimationClip? EmoteClip;
            public EmoteReferences? CurrentEmoteReference;
        }

        public AnimationStates States;
    }
}
