using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Movement.ECS;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings.Rules
{
    public class AnyRule : SendRuleBase
    {
        public List<SendRuleBase> rules;

        public override bool IsSendConditionMet(float t, FullMovementMessage lastFullMovementMessage, ref CharacterAnimationComponent playerAnimationComponent, ref StunComponent playerStunComponent, ref MovementInputComponent move,
            ref JumpInputComponent jump, CharacterController playerCharacter, IMultiplayerMovementSettings settings)
        {
            foreach (var rule in rules)
                if (rule.IsSendConditionMet(t, lastFullMovementMessage, ref playerAnimationComponent, ref playerStunComponent, ref move, ref jump, playerCharacter, settings))
                    return true;

            return false;
        }
    }
}
