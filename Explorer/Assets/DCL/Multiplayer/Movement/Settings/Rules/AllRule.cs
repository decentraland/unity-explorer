using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Movement.ECS;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings.Rules
{
    public class AllRule : SendRuleBase
    {
        public List<SendRuleBase> rules;

        public override string Message => $"$\"<color={color}> VELOCITY ANGLE AND DIFF </color>\"";

        public override bool IsSendConditionMet(float t, FullMovementMessage lastFullMovementMessage, ref CharacterAnimationComponent playerAnimationComponent, ref StunComponent playerStunComponent, ref MovementInputComponent move,
            ref JumpInputComponent jump, CharacterController playerCharacter, IMultiplayerMovementSettings settings)
        {
            var result = false;

            foreach (var rule in rules)
                result = rule.IsSendConditionMet(t, lastFullMovementMessage, ref playerAnimationComponent, ref playerStunComponent, ref move, ref jump, playerCharacter, settings);

            return result;
        }
    }
}
