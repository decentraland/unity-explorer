using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Movement.ECS;
using DCL.Multiplayer.Movement.MessageBusMock;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings
{
    [CreateAssetMenu(fileName = "ProjectiveVelocityDiffExceedRule", menuName = "DCL/Comms/ProjectiveVelocityDiffExceedRule")]
    public class ProjectiveVelocityDiffExceedRuleBase : SendRuleBase
    {
        public override string Message => $"$\"<color={color}> PROJ VELOCITY DIFF </color>\"";

        [Space]
        public float VelocityChangeThreshold;

        public override bool IsSendConditionMet(float t, MessageMock lastMessage, ref CharacterAnimationComponent _, ref StunComponent __, ref MovementInputComponent move,
            ref JumpInputComponent jump, CharacterController playerCharacter,
            IMultiplayerSpatialStateSettings settings)
        {
            Vector3 extrapolatedVelocity = ExtrapolationComponent.DampVelocity(t, lastMessage, settings);
            return Vector3.SqrMagnitude(lastMessage.velocity - extrapolatedVelocity) > VelocityChangeThreshold;
        }
    }
}
