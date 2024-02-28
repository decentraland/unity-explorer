using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Character.Components;
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
    // [LogCategory(ReportCategory.AVATAR)]
    public partial class PlayerNetMovementSendSystem : BaseUnityLoopSystem
    {
        private readonly IArchipelagoIslandRoom room;
        private readonly IMultiplayerSpatialStateSettings settings;

        private readonly CharacterController playerCharacter;

        private float lastSentTime;

        public PlayerNetMovementSendSystem(World world, IArchipelagoIslandRoom room, IMultiplayerSpatialStateSettings settings, CharacterController playerCharacter) : base(world)
        {
            this.room = room;
            this.settings = settings;
            this.playerCharacter = playerCharacter;
        }

        protected override void Update(float t)
        {
            if (!room.IsRunning()) return;

            if (lastSentTime == 0 || UnityEngine.Time.unscaledTime - lastSentTime > settings.PackageSentRate)
                SendPlayerNetMovementQuery(World);
        }

        [Query]
        [All(typeof(PlayerComponent))]
        private void SendPlayerNetMovement(ref CharacterAnimationComponent playerAnimationComponent, ref StunComponent playerStunComponent)
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
