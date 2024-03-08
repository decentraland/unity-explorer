using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Movement.ECS;
using DCL.Multiplayer.Movement.MessageBusMock;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings
{
    [CreateAssetMenu(fileName = "VelocityAngleDiffExceedRule", menuName = "DCL/Comms/VelocityAngleDiffExceedRule")]
    public class VelocityAngleDiffExceedRuleBase : SendRuleBase
    {
        [Space]
        public float VelocityCosAngleDiffInverseThreshold;

        public override string Message => $"$\"<color={color}> VELOCITY ANGLE </color>\"";

        public override bool IsSendConditionMet(float t, FullMovementMessage lastFullMovementMessage, ref CharacterAnimationComponent _, ref StunComponent __, ref MovementInputComponent move,
            ref JumpInputComponent jump, CharacterController playerCharacter,
            IMultiplayerSpatialStateSettings ____) =>
            lastFullMovementMessage.velocity != Vector3.zero && playerCharacter.velocity != Vector3.zero &&
            Vector3.Dot(lastFullMovementMessage.velocity, playerCharacter.velocity) < VelocityCosAngleDiffInverseThreshold;
    }
}
