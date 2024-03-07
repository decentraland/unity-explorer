using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Connections.Rooms.Connective;
using DCL.Multiplayer.Movement.MessageBusMock;
using DCL.Multiplayer.Movement.Settings;
using ECS.Abstract;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Multiplayer.Movement.ECS.System
{
    [UpdateInGroup(typeof(PostRenderingSystemGroup))]
    // [LogCategory(ReportCategory.AVATAR)]
    public partial class PlayerMovementNetSendSystem : BaseUnityLoopSystem
    {
        private readonly IArchipelagoIslandRoom room;
        private readonly IMultiplayerSpatialStateSettings settings;

        private readonly CharacterController playerCharacter;

        private MessageMock? lastSentMessage;

        private int MessagesSentInSec;
        private float mesPerSecTimer;

        public PlayerMovementNetSendSystem(World world, IArchipelagoIslandRoom room, IMultiplayerSpatialStateSettings settings, CharacterController playerCharacter) : base(world)
        {
            this.room = room;
            this.settings = settings;
            this.playerCharacter = playerCharacter;
        }

        protected override void Update(float t)
        {
            UpdateMessagePerSecondCounter(t);

            if (room.CurrentState() == IConnectiveRoom.State.Running)
                SendPlayerNetMovementQuery(World);
        }

        private void UpdateMessagePerSecondCounter(float t)
        {
            if (mesPerSecTimer > 0) { mesPerSecTimer -= t; }
            else
            {
                mesPerSecTimer = 1;
                // Debug.Log($"VVV ------- MES PER SEC: <color={GetColorBasedOnMesPerSec(MessagesSentInSec)}> {MessagesSentInSec} </color> ----------");
                MessagesSentInSec = 0;
            }
        }

        [Query]
        [All(typeof(PlayerComponent))]
        private void SendPlayerNetMovement(ref CharacterAnimationComponent animation, ref StunComponent stun, ref MovementInputComponent move, ref JumpInputComponent jump)
        {
            // UnityEngine.Time.timeScale = settings.TimeScale;
            if(move.Kind == MovementKind.Run) return;

            // Debug.Log($"VVV vel = {playerCharacter.velocity.sqrMagnitude}");
            if (lastSentMessage == null)
            {
                SentMessage(ref animation, ref stun, ref move, ref jump, "FIRST");
                return;
            }

            float timeDiff = UnityEngine.Time.unscaledTime - (lastSentMessage?.timestamp ?? 0);
            // if (MessagesSentInSec >= 10) return;

            foreach (SendRuleBase sendRule in settings.SendRules)
                if (sendRule.IsEnabled
                    && timeDiff > sendRule.MinTimeDelta
                    && sendRule.IsSendConditionMet(timeDiff, lastSentMessage, ref animation, ref stun, ref move, ref jump, playerCharacter, settings))
                {
                    SentMessage(ref animation, ref stun, ref move, ref jump, sendRule.Message);
                    return;
                }
        }

        private void SentMessage(ref CharacterAnimationComponent playerAnimationComponent, ref StunComponent playerStunComponent, ref MovementInputComponent movement, ref JumpInputComponent jump, string from)
        {
            MessagesSentInSec++;

            settings.LastMove = movement.Kind;
            settings.LastJump = jump.IsPressed;

            float deltaTime = UnityEngine.Time.unscaledTime - (lastSentMessage?.timestamp ?? 0);
            string color = GetColorBasedOnDeltaTime(deltaTime);
            // Debug.Log($">VVV {from}: <color={color}> {deltaTime}</color>");

            lastSentMessage = new MessageMock
            {
                timestamp = UnityEngine.Time.unscaledTime,
                position = playerCharacter.transform.position,
                velocity = playerCharacter.velocity,
                animState = playerAnimationComponent.States,
                isStunned = playerStunComponent.IsStunned,
            };

            var byteMessage = new Span<byte>(MessageMockSerializer.SerializeMessage(lastSentMessage));

            IReadOnlyCollection<string>? participants = room is IslandRoomMock ? null : room.Room().Participants.RemoteParticipantSids();
            room.Room().DataPipe.PublishData(byteMessage, "Movement", participants!);
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
