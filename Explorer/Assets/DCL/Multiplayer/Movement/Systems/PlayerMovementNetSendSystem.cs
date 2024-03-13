using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Movement.Settings;
using DCL.Multiplayer.Movement.System;
using ECS.Abstract;
using System;
using System.Collections.Generic;
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
                // Debug.Log($"VVV ------- MES PER SEC: <color={GetColorBasedOnMesPerSec(MessagesSentInSec)}> {MessagesSentInSec} </color> ----------");
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

            // float deltaTime = UnityEngine.Time.unscaledTime - (lastSentMessage?.timestamp ?? 0);
            // string color = GetColorBasedOnDeltaTime(deltaTime);
            // Debug.Log($">VVV {from}: <color={color}> {deltaTime}</color>");

            lastSentMessage = new FullMovementMessage
            {
                timestamp = UnityEngine.Time.unscaledTime,
                position = playerCharacter.transform.position,
                velocity = playerCharacter.velocity,
                animState = playerAnimationComponent.States,
                isStunned = playerStunComponent.IsStunned,
            };

            messageBus.Send(lastSentMessage.Value);

            if (settings.SelfSending)
                messageBus.SelfSendWithDelay(lastSentMessage.Value, settings.Latency + (settings.Latency * Random.Range(0, settings.LatencyJitter)));
        }

        private static string GetColorBasedOnDeltaTime(float deltaTime)
        {
            return deltaTime switch
                   {
                       > 1.49f => "grey",
                       > 0.99f => "lightblue",
                       > 0.49f => "green",
                       > 0.31f => "yellow",
                       > 0.19f => "orange",
                       _ => "red",
                   };
        }

        private static string GetColorBasedOnMesPerSec(int amount)
        {
            return amount switch
                   {
                       > 30 => "red",
                       > 20 => "orange",
                       >= 10 => "yellow",
                       >= 5 => "green",
                       >= 3 => "lightblue",
                       >= 2 => "olive",
                       _ => "grey",
                   };
        }
    }
}
