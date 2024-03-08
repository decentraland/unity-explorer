using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Movement.ECS;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings
{
    public class ProjectivePositionDiffExceedRuleBase : SendRuleBase
    {
        public override string Message => $"$\"<color={color}> PROJ POSITION DIFF </color>\"";

        [Space]
        public float PositionChangeSqrThreshold;

        public override bool IsSendConditionMet(float t, FullMovementMessage lastFullMovementMessage, ref CharacterAnimationComponent _, ref StunComponent __, ref MovementInputComponent move,
            ref JumpInputComponent jump, CharacterController playerCharacter,
            IMultiplayerMovementSettings settings)
        {
            Vector3 extrapolatedVelocity = Extrapolation.DampVelocity(t, lastFullMovementMessage.velocity, settings.ExtrapolationSettings);
            Vector3 projectedPosition = lastFullMovementMessage!.position + (extrapolatedVelocity * t);
            return Vector3.SqrMagnitude(lastFullMovementMessage.position - projectedPosition) > PositionChangeSqrThreshold;
        }
    }
}
