using DCL.CharacterMotion.Components;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings
{
    public class AllRule : SendRuleBase
    {
        public List<SendRuleBase> rules;

        public override string Message => $"$\"<color={color}> ALL RULES </color>\"";

        public override bool IsSendConditionMet(
            in float t,
            in FullMovementMessage lastFullMovementMessage,
            in CharacterAnimationComponent playerAnimationComponent,
            in StunComponent playerStunComponent,
            in MovementInputComponent move,
            in JumpInputComponent jump,
            CharacterController playerCharacter,
            IMultiplayerMovementSettings settings)
        {
            var result = true;

            foreach (SendRuleBase rule in rules)
                if (!rule.IsSendConditionMet(t, lastFullMovementMessage, playerAnimationComponent, playerStunComponent, move, jump, playerCharacter, settings))
                {
                    result = false;
                    break;
                }

            return result;
        }
    }
}
