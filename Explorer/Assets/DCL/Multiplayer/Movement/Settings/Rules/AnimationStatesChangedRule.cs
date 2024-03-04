using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Movement.MessageBusMock;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings
{
    [CreateAssetMenu(fileName = "AnimationStatesChangedRule", menuName = "DCL/Comms/AnimationStatesChangedRule")]
    public class AnimationStatesChangedRuleBase : SendRuleBase
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

        public override bool IsSendConditionMet(float t, MessageMock lastMessage, ref CharacterAnimationComponent playerAnimationComponent, ref StunComponent playerStunComponent, ref MovementInputComponent move,
            ref JumpInputComponent jump, CharacterController ___,
            IMultiplayerSpatialStateSettings ____)
        {
            (bool stateMismatch, string reason)[] checks =
            {
                (Stun && lastMessage.isStunned != playerStunComponent.IsStunned, "STUN"),
                (Jump && lastMessage.animState.IsJumping != playerAnimationComponent.States.IsJumping, "JUMP"),
                (Ground && lastMessage.animState.IsGrounded != playerAnimationComponent.States.IsGrounded, "GROUND"),
                (Fall && lastMessage.animState.IsFalling != playerAnimationComponent.States.IsFalling, "FALL"),
                (LongFall && lastMessage.animState.IsLongFall != playerAnimationComponent.States.IsLongFall, "LONG FALL"),
                (LongJump && lastMessage.animState.IsLongJump != playerAnimationComponent.States.IsLongJump, "LONG JUMP"),
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
