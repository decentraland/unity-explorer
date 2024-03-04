using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Movement.MessageBusMock;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings
{
    [CreateAssetMenu(fileName = "InputJumpPressedChangedRule", menuName = "DCL/Comms/InputJumpPressedChangedRule")]
    public class InputJumpPressedChangedRuleBase : SendRuleBase
    {
        public override string Message => $"$\"<color={color}> INPUT JUMP PRESSED </color>\"";

        public override bool IsSendConditionMet(float t, MessageMock lastMessage, ref CharacterAnimationComponent _, ref StunComponent __,
            ref MovementInputComponent move, ref JumpInputComponent jump, CharacterController ___, IMultiplayerSpatialStateSettings settings) =>
            settings.LastJump != jump.IsPressed;
    }
}
