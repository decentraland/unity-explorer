using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Components;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings
{
    public class ProjectivePositionDiffExceedRule : SendRuleBase
    {
        [Space]
        public float PositionChangeSqrThreshold;
        public override string Message => $"$\"<color={color}> PROJ POSITION DIFF </color>\"";

        public override bool IsSendConditionMet(
            in float t,
            in NetworkMovementMessage lastNetworkMovementMessage,
            in CharacterAnimationComponent playerAnimationComponent,
            in StunComponent playerStunComponent,
            in MovementInputComponent move,
            in JumpInputComponent jump,
            CharacterController playerCharacter,
            IMultiplayerMovementSettings settings)
        {
            RemotePlayerExtrapolationSettings extSettings = settings.ExtrapolationSettings;

            Vector3 extrapolatedVelocity = Extrapolation.DampVelocity(lastNetworkMovementMessage.velocity, t, extSettings.TotalMoveDuration, extSettings.LinearTime);
            Vector3 projectedPosition = lastNetworkMovementMessage!.position + (extrapolatedVelocity * t);
            return Vector3.SqrMagnitude(lastNetworkMovementMessage.position - projectedPosition) > PositionChangeSqrThreshold;
        }
    }
}
