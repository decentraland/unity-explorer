using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Movement.ECS;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings
{
    public class ProjectiveVelocityDiffExceedRuleBase : SendRuleBase
    {
        public override string Message => $"$\"<color={color}> PROJ VELOCITY DIFF </color>\"";

        [Space]
        public float VelocityChangeThreshold;

        public override bool IsSendConditionMet(float t, FullMovementMessage lastFullMovementMessage, ref CharacterAnimationComponent _, ref StunComponent __, ref MovementInputComponent move,
            ref JumpInputComponent jump, CharacterController playerCharacter,
            IMultiplayerMovementSettings settings)
        {
            Vector3 extrapolatedVelocity = Extrapolation.DampVelocity(t, lastFullMovementMessage.velocity, settings.ExtrapolationSettings);
            return Vector3.SqrMagnitude(lastFullMovementMessage.velocity - extrapolatedVelocity) > VelocityChangeThreshold;
        }
    }
}
