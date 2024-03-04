using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Movement.ECS;
using DCL.Multiplayer.Movement.MessageBusMock;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings
{
    [CreateAssetMenu(fileName = "ProjectivePositionDiffExceedRule", menuName = "DCL/Comms/ProjectivePositionDiffExceedRule")]
    public class ProjectivePositionDiffExceedRuleBase : SendRuleBase
    {
        public override string Message => $"$\"<color={color}> PROJ POSITION DIFF </color>\"";

        [Space]
        public float PositionChangeThreshold;

        public override bool IsSendConditionMet(float t, MessageMock lastMessage, ref CharacterAnimationComponent _, ref StunComponent __, ref MovementInputComponent move,
            ref JumpInputComponent jump, CharacterController playerCharacter,
            IMultiplayerSpatialStateSettings settings)
        {
            Vector3 extrapolatedVelocity = ExtrapolationComponent.DampVelocity(t, lastMessage, settings);
            Vector3 projectedPosition = lastMessage!.position + (extrapolatedVelocity * t);
            return Vector3.SqrMagnitude(lastMessage.velocity - projectedPosition) > PositionChangeThreshold;
        }
    }
}
