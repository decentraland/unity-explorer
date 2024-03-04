using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Movement.MessageBusMock;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings
{
    [CreateAssetMenu(fileName = "InputMoveKindChangedRule", menuName = "DCL/Comms/InputMoveKindChangedRule")]
    public class InputMoveKindChangedRuleBase : SendRuleBase
    {
        public override string Message => $"$\"<color={color}> INPUT MOVE KIND </color>\"";

        public override bool IsSendConditionMet(float t, MessageMock lastMessage, ref CharacterAnimationComponent _, ref StunComponent __,
            ref MovementInputComponent move, ref JumpInputComponent jump, CharacterController ___, IMultiplayerSpatialStateSettings settings) =>
            settings.LastMove != move.Kind;
    }
}
