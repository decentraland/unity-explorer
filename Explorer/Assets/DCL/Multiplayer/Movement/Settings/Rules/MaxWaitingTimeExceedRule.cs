using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Movement.MessageBusMock;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings
{
    [CreateAssetMenu(fileName = "MaxWaitingTimeExceedRule", menuName = "DCL/Comms/MaxWaitingTimeExceedRule")]
    public class MaxWaitingTimeExceedRuleBase : SendRuleBase
    {
        public override string Message => $"$\"<color={color}> MAX TIME </color>\"";

        [Space]
        public float MaxSentDelay;

        public override bool IsSendConditionMet(float t, MessageMock lastMessage, ref CharacterAnimationComponent _, ref StunComponent __,
            ref MovementInputComponent move, ref JumpInputComponent jump, CharacterController ___, IMultiplayerSpatialStateSettings ____) =>
            t > MaxSentDelay;
    }
}
