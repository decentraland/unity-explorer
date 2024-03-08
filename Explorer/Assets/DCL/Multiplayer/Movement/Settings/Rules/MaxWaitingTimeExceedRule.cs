using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Movement.ECS;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings
{
    public class MaxWaitingTimeExceedRuleBase : SendRuleBase
    {
        public override string Message => $"$\"<color={color}> MAX TIME </color>\"";

        [Space]
        public float MaxSentDelay;

        public override bool IsSendConditionMet(float t, FullMovementMessage lastFullMovementMessage, ref CharacterAnimationComponent _, ref StunComponent __,
            ref MovementInputComponent move, ref JumpInputComponent jump, CharacterController ___, IMultiplayerMovementSettings ____) =>
            t > MaxSentDelay;
    }
}
