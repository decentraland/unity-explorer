using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Movement.ECS;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings
{
    public class ProjectivePositionDiffExceedRuleBase : SendRuleBase
    {
        [Space]
        public float PositionChangeSqrThreshold;
        public override string Message => $"$\"<color={color}> PROJ POSITION DIFF </color>\"";

        public override bool IsSendConditionMet(float t, FullMovementMessage lastFullMovementMessage, ref CharacterAnimationComponent _, ref StunComponent __, ref MovementInputComponent move,
            ref JumpInputComponent jump, CharacterController playerCharacter,
            IMultiplayerMovementSettings settings)
        {
            RemotePlayerExtrapolationSettings extSettings = settings.ExtrapolationSettings;

            Vector3 extrapolatedVelocity = Extrapolation.DampVelocity(lastFullMovementMessage.velocity, t, extSettings.TotalMoveDuration, extSettings.LinearTime);
            Vector3 projectedPosition = lastFullMovementMessage!.position + (extrapolatedVelocity * t);
            return Vector3.SqrMagnitude(lastFullMovementMessage.position - projectedPosition) > PositionChangeSqrThreshold;
        }
    }
}
