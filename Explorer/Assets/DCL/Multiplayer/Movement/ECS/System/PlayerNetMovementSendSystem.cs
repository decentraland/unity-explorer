using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Movement.MessageBusMock;
using DCL.Multiplayer.Movement.Settings;
using ECS.Abstract;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.ActiveSpeakers;
using LiveKit.Rooms.DataPipes;
using LiveKit.Rooms.Participants;
using LiveKit.Rooms.Tracks.Hub;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
            if (!room.EnsuredIsRunning()) return;

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

                var byteMessage = new Span<byte>(SerializeMessage(message));
                // IReadOnlyCollection<string> participants = room.Room().Participants.RemoteParticipantSids();

                room.Room().DataPipe.PublishData(byteMessage, "Movement", null!);
            }
        }

        public byte[] SerializeMessage(MessageMock message)
        {
            string? json = JsonUtility.ToJson(message);
            return Encoding.UTF8.GetBytes(json);
        }

        public MessageMock DeserializeMessage(Span<byte> data)
        {
            string jsonString = Encoding.UTF8.GetString(data.ToArray());
            return JsonUtility.FromJson<MessageMock>(jsonString);
        }
    }

    public class IslandRoomMock : IArchipelagoIslandRoom, IRoom, IDataPipe
    {
        public IActiveSpeakers ActiveSpeakers { get; }

        public IParticipantsHub Participants { get; }

        public IDataPipe DataPipe => this;
        public event ReceivedDataDelegate? DataReceived;

        public IRoom Room() =>
            this;

        public void PublishData(Span<byte> data, string topic, IReadOnlyCollection<string> destinationSids, DataPacketKind kind = DataPacketKind.KindLossy)
        {
            DataReceived?.Invoke(data, new Participant(), kind);
        }

#region MyRegion
        public event Room.MetaDelegate? RoomMetadataChanged;

        public event LocalPublishDelegate? LocalTrackPublished;
        public event LocalPublishDelegate? LocalTrackUnpublished;
        public event PublishDelegate? TrackPublished;
        public event PublishDelegate? TrackUnpublished;
        public event SubscribeDelegate? TrackSubscribed;
        public event SubscribeDelegate? TrackUnsubscribed;
        public event MuteDelegate? TrackMuted;
        public event MuteDelegate? TrackUnmuted;
        public event ConnectionQualityChangeDelegate? ConnectionQualityChanged;
        public event ConnectionStateChangeDelegate? ConnectionStateChanged;
        public event ConnectionDelegate? ConnectionUpdated;

        public void Start() { }

        public void Stop() { }

        public bool IsRunning() =>
            true;

        public Task<bool> Connect(string url, string authToken, CancellationToken cancelToken) =>
            Task.FromResult(true);

        public void Disconnect() { }
#endregion
    }
}
