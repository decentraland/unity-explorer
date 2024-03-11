using DCL.CharacterMotion.Components;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings
{
    public class InputJumpPressedChangedRuleBase : SendRuleBase
    {
        public override string Message => $"$\"<color={color}> INPUT JUMP PRESSED </color>\"";

        public override bool IsSendConditionMet(float t, FullMovementMessage lastFullMovementMessage, ref CharacterAnimationComponent _, ref StunComponent __,
            ref MovementInputComponent move, ref JumpInputComponent jump, CharacterController ___, IMultiplayerMovementSettings settings) =>
            settings.LastJump != jump.IsPressed;
    }
}
