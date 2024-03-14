using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.Multiplayer.Movement.Settings;
using DCL.Multiplayer.Movement.System;
using ECS.Abstract;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DCL.Multiplayer.Movement.Systems
{
    [UpdateInGroup(typeof(PostRenderingSystemGroup))]
    [LogCategory(ReportCategory.MULTIPLAYER_MOVEMENT)]
    public partial class PlayerMovementNetSendSystem : BaseUnityLoopSystem
    {
        private const int MAX_MESSAGES_PER_SEC = 10; // 10 Hz == 10 [msg/sec]

        private readonly MultiplayerMovementMessageBus messageBus;
        private readonly IMultiplayerMovementSettings settings;

        private readonly CharacterController playerCharacter;

        private FullMovementMessage? lastSentMessage;

        private int messagesSentInSec;
        private float mesPerSecResetCooldown;

        public PlayerMovementNetSendSystem(World world, MultiplayerMovementMessageBus messageBus, IMultiplayerMovementSettings settings, CharacterController playerCharacter) : base(world)
        {
            this.messageBus = messageBus;
            this.settings = settings;
            this.playerCharacter = playerCharacter;
        }

        protected override void Update(float t)
        {
            UpdateMessagePerSecondCounter(t);
            SendPlayerNetMovementQuery(World);
        }

        private void UpdateMessagePerSecondCounter(float t)
        {
            if (mesPerSecResetCooldown > 0)
                mesPerSecResetCooldown -= t;
            else
            {
                mesPerSecResetCooldown = 1; // 1 [sec]
                messagesSentInSec = 0;
            }
        }

        [Query]
        [All(typeof(PlayerComponent))]
        private void SendPlayerNetMovement(ref CharacterAnimationComponent animation, ref StunComponent stun, ref MovementInputComponent move, ref JumpInputComponent jump)
        {
            if (messagesSentInSec >= MAX_MESSAGES_PER_SEC) return;

            if (lastSentMessage == null)
            {
                SendMessage(ref animation, ref stun, ref move, ref jump, "FIRST");
                return;
            }

            float timeDiff = UnityEngine.Time.unscaledTime - lastSentMessage!.Value.timestamp;

            foreach (SendRuleBase sendRule in settings.SendRules)
                if (timeDiff > sendRule.MinTimeDelta
                    && sendRule.IsSendConditionMet(timeDiff, lastSentMessage!.Value, ref animation, ref stun, ref move, ref jump, playerCharacter, settings))
                {
                    SendMessage(ref animation, ref stun, ref move, ref jump, sendRule.Message);
                    return;
                }
        }

        private void SendMessage(ref CharacterAnimationComponent playerAnimationComponent, ref StunComponent playerStunComponent, ref MovementInputComponent movement, ref JumpInputComponent jump, string from)
        {
            messagesSentInSec++;

            lastSentMessage = new FullMovementMessage
            {
                timestamp = UnityEngine.Time.unscaledTime,
                position = playerCharacter.transform.position,
                velocity = playerCharacter.velocity,
                animState = playerAnimationComponent.States,
                isStunned = playerStunComponent.IsStunned,
            };

            messageBus.Send(lastSentMessage.Value);

            if (settings.SelfSending && movement.Kind != MovementKind.Run)
                messageBus.SelfSendWithDelay(lastSentMessage.Value, settings.Latency + (settings.Latency * Random.Range(0, settings.LatencyJitter))).Forget();
        }
    }
}
