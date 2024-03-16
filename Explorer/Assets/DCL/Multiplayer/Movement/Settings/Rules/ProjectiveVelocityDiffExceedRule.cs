using DCL.CharacterMotion.Components;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings.Rules
{
    public class ProjectiveVelocityDiffExceedRule : SendRuleBase
    {
        [Space]
        public float VelocityChangeThreshold;
        public override string Message => $"$\"<color={color}> PROJ VELOCITY DIFF </color>\"";

        public override bool IsSendConditionMet(
            in float t,
            in FullMovementMessage lastFullMovementMessage,
            in CharacterAnimationComponent playerAnimationComponent,
            in StunComponent playerStunComponent,
            in MovementInputComponent move,
            in JumpInputComponent jump,
            CharacterController playerCharacter,
            IMultiplayerMovementSettings settings)
        {
            RemotePlayerExtrapolationSettings extSettings = settings.ExtrapolationSettings;

            Vector3 extrapolatedVelocity = Extrapolation.DampVelocity(lastFullMovementMessage.velocity, t, extSettings.TotalMoveDuration, extSettings.LinearTime);
            return Vector3.SqrMagnitude(lastFullMovementMessage.velocity - extrapolatedVelocity) > VelocityChangeThreshold;
        }
    }
}
