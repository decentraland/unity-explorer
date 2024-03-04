using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Movement.MessageBusMock;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings
{
    [CreateAssetMenu(fileName = "VelocityAngleDiffExceedRule", menuName = "DCL/Comms/VelocityAngleDiffExceedRule")]
    public class VelocityAngleDiffExceedRuleBase : SendRuleBase
    {
        public override string Message => $"$\"<color={color}> VELOCITY ANGLE </color>\"";

        [Space]
        public float VelocityCosAngleDiffInverseThreshold;

        public override bool IsSendConditionMet(float t, MessageMock lastMessage, ref CharacterAnimationComponent _, ref StunComponent __, ref MovementInputComponent move,
            ref JumpInputComponent jump, CharacterController playerCharacter,
            IMultiplayerSpatialStateSettings ____) =>
            lastMessage.velocity != Vector3.zero &&
            playerCharacter.velocity != Vector3.zero &&
            Vector3.Dot(lastMessage.velocity.normalized, playerCharacter.velocity.normalized) < VelocityCosAngleDiffInverseThreshold;
    }
}
