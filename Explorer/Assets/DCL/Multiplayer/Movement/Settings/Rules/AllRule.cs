using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Movement.MessageBusMock;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings.Rules
{
    [CreateAssetMenu(fileName = "AllRule", menuName = "DCL/Comms/AllRule")]
    public class AllRule : SendRuleBase
    {
        public List<SendRuleBase> rules;

        public override string Message => $"$\"<color={color}> VELOCITY ANGLE AND DIFF </color>\"";

        public override bool IsSendConditionMet(float t, MessageMock lastMessage, ref CharacterAnimationComponent playerAnimationComponent, ref StunComponent playerStunComponent, ref MovementInputComponent move,
            ref JumpInputComponent jump, CharacterController playerCharacter, IMultiplayerSpatialStateSettings settings)
        {
            var result = false;

            foreach (var rule in rules)
                result = rule.IsSendConditionMet(t, lastMessage, ref playerAnimationComponent, ref playerStunComponent, ref move, ref jump, playerCharacter, settings);

            return result;
        }
    }
}
