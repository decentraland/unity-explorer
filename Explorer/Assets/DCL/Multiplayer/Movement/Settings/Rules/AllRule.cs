using DCL.CharacterMotion.Components;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings
{
    public class AllRule : SendRuleBase
    {
        public List<SendRuleBase> rules;

        public override string Message => $"$\"<color={color}> ALL RULES </color>\"";

        public override bool IsSendConditionMet(float t, FullMovementMessage lastFullMovementMessage, ref CharacterAnimationComponent playerAnimationComponent, ref StunComponent playerStunComponent, ref MovementInputComponent move,
            ref JumpInputComponent jump, CharacterController playerCharacter, IMultiplayerMovementSettings settings)
        {
            var result = true;

            foreach (SendRuleBase rule in rules)
                if (!rule.IsSendConditionMet(t, lastFullMovementMessage, ref playerAnimationComponent, ref playerStunComponent, ref move, ref jump, playerCharacter, settings))
                {
                    result = false;
                    break;
                }

            return result;
        }
    }
}
