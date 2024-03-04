using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Movement.MessageBusMock;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings
{
    [CreateAssetMenu(fileName = "AnimationStatesChangedRule", menuName = "DCL/Comms/AnimationStatesChangedRule")]
    public class AnimationStatesChangedRuleBase : SendRuleBase
    {
        private string reason = string.Empty;
        public override string Message => $"<color={color}> ANIM {reason} </color>";

        public override bool IsSendConditionMet(float t, MessageMock lastMessage, ref CharacterAnimationComponent playerAnimationComponent, ref StunComponent playerStunComponent, ref MovementInputComponent move,
            ref JumpInputComponent jump, CharacterController ___,
            IMultiplayerSpatialStateSettings ____)
        {
            (bool stateMismatch, string reason)[] checks =
            {
                (lastMessage.isStunned != playerStunComponent.IsStunned, "STUN"),
                (lastMessage.animState.IsJumping != playerAnimationComponent.States.IsJumping, "JUMP"),
                (lastMessage.animState.IsGrounded != playerAnimationComponent.States.IsGrounded, "GROUND"),
                (lastMessage.animState.IsFalling != playerAnimationComponent.States.IsFalling, "FALL"),
                (lastMessage.animState.IsLongFall != playerAnimationComponent.States.IsLongFall, "LONG FALL"),
                (lastMessage.animState.IsLongJump != playerAnimationComponent.States.IsLongJump, "LONG JUMP"),
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
