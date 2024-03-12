using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Movement.Settings;
using LiveKit.Proto;
using LiveKit.Rooms.Participants;
using System;
using System.Collections.Generic;
using Utility.PriorityQueue;
using Random = UnityEngine.Random;

namespace DCL.Multiplayer.Movement.System
{
    public class RemotePlayersMovementInbox
    {
        public readonly Dictionary<string, SimplePriorityQueue<FullMovementMessage>> InboxByParticipantMap = new ();

        private readonly IRoomHub roomHub;
        private readonly IMultiplayerMovementSettings settings;

        public RemotePlayersMovementInbox(IRoomHub roomHub, IMultiplayerMovementSettings settings)
        {
            this.roomHub = roomHub;
            this.settings = settings;

            if (roomHub.IslandRoom() is IslandRoomMock)
            {
                roomHub.IslandRoom().DataPipe.DataReceived += InboxSelfMessageWithDelay;
                // roomHub.SceneRoom().DataPipe.DataReceived += InboxSelfMessageWithDelay;
            }
            else
            {
                roomHub.IslandRoom().DataPipe.DataReceived += InboxDeserializedMessage;
                roomHub.SceneRoom().DataPipe.DataReceived += InboxDeserializedMessage;
            }
        }

        ~RemotePlayersMovementInbox()
        {
            if (roomHub.IslandRoom() is IslandRoomMock)
            {
                roomHub.IslandRoom().DataPipe.DataReceived -= InboxSelfMessageWithDelay;
                // roomHub.SceneRoom().DataPipe.DataReceived -= InboxSelfMessageWithDelay;
            }
            else
            {
                roomHub.IslandRoom().DataPipe.DataReceived -= InboxDeserializedMessage;
                roomHub.SceneRoom().DataPipe.DataReceived -= InboxDeserializedMessage;
            }
        }

        private void InboxSelfMessageWithDelay(ReadOnlySpan<byte> data, Participant participant, DataPacketKind kind)
        {
            FullMovementMessage? message = FullMovementMessageSerializer.DeserializeMessage(data);

            if (message != null)
                UniTask.Delay(TimeSpan.FromSeconds(settings.Latency + (settings.Latency * Random.Range(0, settings.LatencyJitter))))
                       .ContinueWith(() => Inbox(message.Value, @for: RemotePlayerMovementComponent.TEST_ID))
                       .Forget();
        }

        private void InboxDeserializedMessage(ReadOnlySpan<byte> data, Participant participant, DataPacketKind kind)
        {
            FullMovementMessage? message = FullMovementMessageSerializer.DeserializeMessage(data);

            if (message != null)
                Inbox(message.Value, @for: participant.Identity); // TODO (Vit): filter out Island messages if Participant is presented in the Room
        }

        private void Inbox(FullMovementMessage fullMovementMessage, string @for)
        {
            if (InboxByParticipantMap.TryGetValue(@for, out SimplePriorityQueue<FullMovementMessage>? queue) && !queue.Contains(fullMovementMessage))
                queue.Enqueue(fullMovementMessage, fullMovementMessage.timestamp);
            else
            {
                var newQueue = new SimplePriorityQueue<FullMovementMessage>(); // TODO (Vit): pooling
                newQueue.Enqueue(fullMovementMessage, fullMovementMessage.timestamp);

                InboxByParticipantMap.Add(@for, newQueue);
            }
        }
    }
}
