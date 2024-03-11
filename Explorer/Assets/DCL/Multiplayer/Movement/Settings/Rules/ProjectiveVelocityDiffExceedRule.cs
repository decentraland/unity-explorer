using DCL.CharacterMotion.Components;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings.Rules
{
    public class ProjectiveVelocityDiffExceedRuleBase : SendRuleBase
    {
        [Space]
        public float VelocityChangeThreshold;
        public override string Message => $"$\"<color={color}> PROJ VELOCITY DIFF </color>\"";

        public override bool IsSendConditionMet(float t, FullMovementMessage lastFullMovementMessage, ref CharacterAnimationComponent _, ref StunComponent __, ref MovementInputComponent move,
            ref JumpInputComponent jump, CharacterController playerCharacter,
            IMultiplayerMovementSettings settings)
        {
            RemotePlayerExtrapolationSettings extSettings = settings.ExtrapolationSettings;

            Vector3 extrapolatedVelocity = Extrapolation.DampVelocity(lastFullMovementMessage.velocity, t, extSettings.TotalMoveDuration, extSettings.LinearTime);
            return Vector3.SqrMagnitude(lastFullMovementMessage.velocity - extrapolatedVelocity) > VelocityChangeThreshold;
        }
    }
}
