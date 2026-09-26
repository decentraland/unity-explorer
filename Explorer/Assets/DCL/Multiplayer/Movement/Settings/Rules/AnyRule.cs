using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Components;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings.Rules
{
    public class AnyRule : SendRuleBase
    {
        public List<SendRuleBase> rules;

        public override bool IsSendConditionMet(
            in float t,
            in NetworkMovementMessage lastNetworkMovementMessage,
            in CharacterAnimationComponent playerAnimationComponent,
            in StunComponent playerStunComponent,
            in MovementInputComponent move,
            in JumpInputComponent jump,
            CharacterController playerCharacter,
            IMultiplayerMovementSettings settings)
        {
            foreach (var rule in rules)
                if (rule.IsSendConditionMet(t, in lastNetworkMovementMessage, in playerAnimationComponent, in playerStunComponent, in move, in jump, playerCharacter, settings))
                    return true;

            return false;
        }
    }
}
