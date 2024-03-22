using DCL.CharacterMotion.Components;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings
{
    public class AnimationStatesChangedRule : SendRuleBase
    {
        [Space]
        public bool Stun = true;
        public bool Jump = true;
        public bool Ground = true;
        public bool Fall = true;
        public bool LongFall = true;
        public bool LongJump = true;

        private string reason = string.Empty;
        public override string Message => $"<color={color}> ANIM {reason} </color>";

        public override bool IsSendConditionMet(
            in float t,
            in FullMovementMessage lastFullMovementMessage,
            in CharacterAnimationComponent playerAnimationComponent,
            in StunComponent playerStunComponent,
            in MovementInputComponent move,
            in JumpInputComponent jump,
            CharacterController playerCharacter,
            IMultiplayerMovementSettings settings)
        {
            (bool stateMismatch, string reason)[] checks =
            {
                (Stun && lastFullMovementMessage.isStunned != playerStunComponent.IsStunned, "STUN"),
                (Jump && lastFullMovementMessage.animState.IsJumping != playerAnimationComponent.States.IsJumping, "JUMP"),
                (Ground && lastFullMovementMessage.animState.IsGrounded != playerAnimationComponent.States.IsGrounded, "GROUND"),
                (Fall && lastFullMovementMessage.animState.IsFalling != playerAnimationComponent.States.IsFalling, "FALL"),
                (LongFall && lastFullMovementMessage.animState.IsLongFall != playerAnimationComponent.States.IsLongFall, "LONG FALL"),
                (LongJump && lastFullMovementMessage.animState.IsLongJump != playerAnimationComponent.States.IsLongJump, "LONG JUMP"),
            };

            foreach ((bool stateMismatch, string reason) check in checks)
                if (check.stateMismatch)
                {
                    reason = check.reason;
                    return true;
                }

            reason = string.Empty;
            return false;
        }
    }
}
