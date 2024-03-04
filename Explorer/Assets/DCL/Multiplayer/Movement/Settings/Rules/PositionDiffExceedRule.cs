using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Movement.MessageBusMock;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings
{
    [CreateAssetMenu(fileName = "PositionDiffExceedRule", menuName = "DCL/Comms/PositionDiffExceedRule")]
    public class PositionDiffExceedRuleBase : SendRuleBase
    {
        public override string Message => $"$\"<color={color}> POSITION DIFF </color>\"";

        [Space]
        public float PositionChangeThreshold;

        public override bool IsSendConditionMet(float t, MessageMock lastMessage, ref CharacterAnimationComponent _, ref StunComponent __, ref MovementInputComponent move,
            ref JumpInputComponent jump, CharacterController playerCharacter,
            IMultiplayerSpatialStateSettings ____) =>
            Vector3.SqrMagnitude(lastMessage.position - playerCharacter.transform.position) > PositionChangeThreshold;
    }
}
