using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Components;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings
{
    public class PositionDiffExceedRule : SendRuleBase
    {
        [Space]
        public float PositionChangeThreshold;
        public override string Message => $"$\"<color={color}> POSITION DIFF </color>\"";

        public override bool IsSendConditionMet(
            in float t,
            in FullMovementMessage lastFullMovementMessage,
            in CharacterAnimationComponent playerAnimationComponent,
            in StunComponent playerStunComponent,
            in MovementInputComponent move,
            in JumpInputComponent jump,
            CharacterController playerCharacter,
            IMultiplayerMovementSettings settings) =>
            Vector3.SqrMagnitude(lastFullMovementMessage.position - playerCharacter.transform.position) > PositionChangeThreshold;
    }
}
