using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Movement.ECS;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings
{
    public class VelocityDiffExceedRuleBase : SendRuleBase
    {
        public override string Message => $"$\"<color={color}> VELOCITY DIFF </color>\"";

        [Space]
        public float VelocityChangeThreshold;

        public override bool IsSendConditionMet(float t, FullMovementMessage lastFullMovementMessage, ref CharacterAnimationComponent _, ref StunComponent __, ref MovementInputComponent move,
            ref JumpInputComponent jump, CharacterController playerCharacter,
            IMultiplayerMovementSettings ____) =>
            Vector3.SqrMagnitude(lastFullMovementMessage.velocity - playerCharacter.velocity) > VelocityChangeThreshold;
    }
}
