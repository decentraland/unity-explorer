using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Components;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings
{
    public class AnimationMovementBlendChangedRule : SendRuleBase
    {
        public override string Message => $"<color={color}> ANIM {reason} </color>";

        [Space]
        public float MoveBlendTiersDiff = 1;
        public float MinSlideBlendDiff = 1;

        private string reason = string.Empty;

        public override bool IsSendConditionMet(
            in float t,
            in NetworkMovementMessage lastNetworkMovementMessage,
            in CharacterAnimationComponent playerAnimationComponent,
            in StunComponent playerStunComponent,
            in MovementInputComponent move,
            in JumpInputComponent jump,
            CharacterController playerCharacter,
            IMultiplayerMovementSettings settings)
        {
            // Maybe we don't need it because of velocity change?
            if (Mathf.Abs(GetMovementBlendTier(lastNetworkMovementMessage.animState.MovementBlendValue) - GetMovementBlendTier(playerAnimationComponent.States.MovementBlendValue)) >= MoveBlendTiersDiff)
            {
                reason = $"MOVEMENT {GetMovementBlendTier(lastNetworkMovementMessage.animState.MovementBlendValue)} vs {GetMovementBlendTier(playerAnimationComponent.States.MovementBlendValue)}";
                return true;
            }

            if (Mathf.Abs(lastNetworkMovementMessage.animState.SlideBlendValue - playerAnimationComponent.States.SlideBlendValue) > MinSlideBlendDiff)
            {
                reason = "SLIDE";
                return true;
            }

            reason = string.Empty;
            return false;
        }

        // state idle ----- walk ----- jog ----- run
        // blend  0  -----   1  -----  2  -----  3
        private static int GetMovementBlendTier(float value) =>
            value switch
            {
                < 1 => 0,
                < 2 => 1,
                < 3 => 2,
                _ => 3,
            };
    }
}
