using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Movement.ECS;
using DCL.Multiplayer.Movement.MessageBusMock;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings
{
    [CreateAssetMenu(fileName = "VelocityDiffExceedRule", menuName = "DCL/Comms/VelocityDiffExceedRule")]
    public class VelocityDiffExceedRuleBase : SendRuleBase
    {
        public override string Message => $"$\"<color={color}> VELOCITY DIFF </color>\"";

        [Space]
        public float VelocityChangeThreshold;

        public override bool IsSendConditionMet(float t, FullMovementMessage lastFullMovementMessage, ref CharacterAnimationComponent _, ref StunComponent __, ref MovementInputComponent move,
            ref JumpInputComponent jump, CharacterController playerCharacter,
            IMultiplayerSpatialStateSettings ____) =>
            Vector3.SqrMagnitude(lastFullMovementMessage.velocity - playerCharacter.velocity) > VelocityChangeThreshold;
    }
}
