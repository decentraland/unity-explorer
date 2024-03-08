using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Movement.ECS;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings
{
    public class PositionDiffExceedRuleBase : SendRuleBase
    {
        public override string Message => $"$\"<color={color}> POSITION DIFF </color>\"";

        [Space]
        public float PositionChangeThreshold;

        public override bool IsSendConditionMet(float t, FullMovementMessage lastFullMovementMessage, ref CharacterAnimationComponent _, ref StunComponent __, ref MovementInputComponent move,
            ref JumpInputComponent jump, CharacterController playerCharacter,
            IMultiplayerMovementSettings ____) =>
            Vector3.SqrMagnitude(lastFullMovementMessage.position - playerCharacter.transform.position) > PositionChangeThreshold;
    }
}
