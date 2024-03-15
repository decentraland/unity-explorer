using DCL.CharacterMotion.Components;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings
{
    public class VelocityAngleDiffExceedRule : SendRuleBase
    {
        [Space]
        public float VelocityCosAngleDiffInverseThreshold;

        public override string Message => $"$\"<color={color}> VELOCITY ANGLE </color>\"";

        public override bool IsSendConditionMet(
            in float t,
            in FullMovementMessage lastFullMovementMessage,
            in CharacterAnimationComponent playerAnimationComponent,
            in StunComponent playerStunComponent,
            in MovementInputComponent move,
            in JumpInputComponent jump,
            CharacterController playerCharacter,
            IMultiplayerMovementSettings settings) =>
            lastFullMovementMessage.velocity != Vector3.zero && playerCharacter.velocity != Vector3.zero &&
            Vector3.Dot(lastFullMovementMessage.velocity, playerCharacter.velocity) < VelocityCosAngleDiffInverseThreshold;
    }
}
