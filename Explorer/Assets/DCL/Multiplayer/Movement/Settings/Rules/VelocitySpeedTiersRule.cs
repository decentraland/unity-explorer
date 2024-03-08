using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Movement.ECS;
using DCL.Multiplayer.Movement.MessageBusMock;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings
{
    [CreateAssetMenu(fileName = "VelocitySpeedTiersRule", menuName = "DCL/Comms/VelocitySpeedTiersRule")]
    public class VelocitySpeedTiersRuleBase : SendRuleBase
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

        public override bool IsSendConditionMet(float t, FullMovementMessage lastFullMovementMessage, ref CharacterAnimationComponent _, ref StunComponent __, ref MovementInputComponent move,
            ref JumpInputComponent jump, CharacterController playerCharacter,
            IMultiplayerSpatialStateSettings ____)
        {
            // Velocity tiers - 0 = idle, 1 = walk, 2 = run, 3 = sprint
            (float Threshold, float Rate, string Reason)[] conditions = new[]
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
