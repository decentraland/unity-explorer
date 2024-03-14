using DCL.CharacterMotion.Components;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings
{
    public class VelocityDiffExceedRule : SendRuleBase
    {
        public override string Message => $"$\"<color={color}> VELOCITY DIFF </color>\"";

        [Space]
        public float VelocityChangeThreshold;

        public override bool IsSendConditionMet(
            in float t,
            in FullMovementMessage lastFullMovementMessage,
            in CharacterAnimationComponent playerAnimationComponent,
            in StunComponent playerStunComponent,
            in MovementInputComponent move,
            in JumpInputComponent jump,
            CharacterController playerCharacter,
            IMultiplayerMovementSettings settings) =>
            Vector3.SqrMagnitude(lastFullMovementMessage.velocity - playerCharacter.velocity) > VelocityChangeThreshold;
    }
}
