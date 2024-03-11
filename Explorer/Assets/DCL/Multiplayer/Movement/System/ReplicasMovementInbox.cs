using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Movement.Settings;
using LiveKit.Proto;
using LiveKit.Rooms.Participants;
using System;
using System.Collections.Generic;
using Random = UnityEngine.Random;

namespace DCL.Multiplayer.Movement.ECS.System
{
    public class RemotePlayersMovementInbox
    {
        public readonly Dictionary<string, Queue<FullMovementMessage>> InboxByParticipantMap = new ();
        private readonly Queue<FullMovementMessage> incomingMessages = new ();

        private readonly IRoomHub roomHub;
        private readonly IMultiplayerMovementSettings settings;

        private FullMovementMessage? lastMessage;

        public RemotePlayersMovementInbox(IRoomHub roomHub, IMultiplayerMovementSettings settings)
        {
            this.roomHub = roomHub;
            this.settings = settings;

            if (roomHub.IslandRoom() is IslandRoomMock) { roomHub.IslandRoom().DataPipe.DataReceived += InboxDeserializedMessageMock; }
            else
            {
                roomHub.SceneRoom().DataPipe.DataReceived += InboxDeserializedMessage;
                roomHub.IslandRoom().DataPipe.DataReceived += InboxDeserializedMessage;
            }
        }

        ~RemotePlayersMovementInbox()
        {
            if (roomHub.IslandRoom() is IslandRoomMock) { roomHub.IslandRoom().DataPipe.DataReceived -= InboxDeserializedMessageMock; }
            else
            {
                roomHub.SceneRoom().DataPipe.DataReceived -= InboxDeserializedMessage;
                roomHub.IslandRoom().DataPipe.DataReceived -= InboxDeserializedMessage;
            }
        }

        private void InboxDeserializedMessageMock(ReadOnlySpan<byte> data, Participant participant, DataPacketKind kind)
        {
            FullMovementMessage? message = MessageMockSerializer.DeserializeMessage(data);

            if (message == null) return;

            float sentRate = lastMessage == null ? 1f : message.timestamp - lastMessage.timestamp;
            lastMessage = message;

            UniTask.Delay(TimeSpan.FromSeconds(settings.Latency
                                               + (settings.Latency * Random.Range(0, settings.LatencyJitter))
                                               + (sentRate * Random.Range(0, settings.PackagesJitter))))
                   .ContinueWith(() =>
                        Inbox(message, @for: RemotePlayerMovementComponent.TEST_ID))
                   .Forget();
        }

        private void InboxDeserializedMessage(ReadOnlySpan<byte> data, Participant participant, DataPacketKind kind)
        {
            FullMovementMessage? message = MessageMockSerializer.DeserializeMessage(data);

            if (message != null)
                Inbox(message, @for: participant.Identity);
        }

        private void Inbox(FullMovementMessage fullMovementMessage, string @for)
        {
            if (InboxByParticipantMap.TryGetValue(@for, out Queue<FullMovementMessage>? queue))
                queue.Enqueue(fullMovementMessage);
            else
            {
                var newQueue = new Queue<FullMovementMessage>();
                newQueue.Enqueue(fullMovementMessage);

                InboxByParticipantMap.Add(@for, newQueue);
            }
        }
    }
}
