using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Components;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings
{
    public class MaxWaitingTimeExceedRule : SendRuleBase
    {
        [Space]
        public float MaxSentDelay;
        public override string Message => $"$\"<color={color}> MAX TIME </color>\"";

        public override bool IsSendConditionMet(
            in float t,
            in NetworkMovementMessage _,
            in CharacterAnimationComponent __,
            in StunComponent ___,
            in MovementInputComponent ____,
            in JumpInputComponent _____,
            CharacterController ______,
            IMultiplayerMovementSettings _______) =>
            t > MaxSentDelay;
    }
}
