using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Movement.MessageBusMock;
using DCL.Multiplayer.Movement.Settings;
using ECS.Abstract;
using System;
using UnityEngine;

namespace DCL.Multiplayer.Movement.ECS.System
{
    [UpdateInGroup(typeof(PostRenderingSystemGroup))]
    public partial class PlayerNetMovementSendSystem : BaseUnityLoopSystem
    {
        private readonly IArchipelagoIslandRoom room;
        private readonly IMultiplayerSpatialStateSettings settings;

        private readonly CharacterController playerCharacter;
        private readonly CharacterAnimationComponent playerAnimationComponent;
        private readonly StunComponent playerStunComponent;

        private float lastSentTime;

        public PlayerNetMovementSendSystem(World world, IArchipelagoIslandRoom room, IMultiplayerSpatialStateSettings settings, CharacterController playerCharacter,
            CharacterAnimationComponent playerAnimationComponent, StunComponent playerStunComponent) : base(world)
        {
            this.room = room;
            this.settings = settings;
            this.playerCharacter = playerCharacter;
            this.playerAnimationComponent = playerAnimationComponent;
            this.playerStunComponent = playerStunComponent;
        }

        protected override void Update(float t)
        {
            if (!room.IsRunning()) return;

            if (lastSentTime == 0 || UnityEngine.Time.unscaledTime - lastSentTime > settings.PackageSentRate)
            {
                lastSentTime = UnityEngine.Time.unscaledTime;

                var message = new MessageMock
                {
                    timestamp = lastSentTime,
                    position = playerCharacter.transform.position,
                    velocity = playerCharacter.velocity,
                    animState = playerAnimationComponent.States,
                    isStunned = playerStunComponent.IsStunned,
                };

                var byteMessage = new Span<byte>(MessageMockSerializer.SerializeMessage(message));
                // IReadOnlyCollection<string> participants = room.Room().Participants.RemoteParticipantSids();
                room.Room().DataPipe.PublishData(byteMessage, "Movement", null!);
            }
        }
    }
}
