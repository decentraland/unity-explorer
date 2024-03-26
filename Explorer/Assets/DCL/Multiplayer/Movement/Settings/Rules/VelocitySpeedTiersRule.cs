using DCL.CharacterMotion.Components;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings
{
    public class VelocitySpeedTiersRule : SendRuleBase
    {
        [Header("SPEED SENT RATES")]
        public float SprintSentRate;
        public float RunSentRate;
        public float WalkSentRate;

        [Header("SPEED THRESHOLDS")]
        public float SprintSqrSpeed;
        public float RunSqrSpeed;
        public float WalkSqrSpeed;

        private string reason = string.Empty;
        public override string Message => $"$\"<color={color}> VELOCITY TIERS {reason}</color>\"";

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
            // Velocity tiers - 0 = idle, 1 = walk, 2 = run, 3 = sprint
            (float Threshold, float Rate, string Reason)[] conditions =
            {
                (Threshold: SprintSqrSpeed, Rate: SprintSentRate, Reason: "SPRINT"),
                (Threshold: RunSqrSpeed, Rate: RunSentRate, Reason: "RUN"),
                (Threshold: WalkSqrSpeed, Rate: WalkSentRate, Reason: "WALK"),
            };

            // Check each condition in descending order of velocity
            foreach ((float Threshold, float Rate, string Reason) condition in conditions)
                if (playerCharacter.velocity.sqrMagnitude > condition.Threshold && t > condition.Rate)
                {
                    reason = condition.Reason;
                    return true;
                }

            reason = string.Empty;
            return false;
        }
    }
}
