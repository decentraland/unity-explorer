using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Movement.MessageBusMock;
using DCL.Multiplayer.Movement.Settings;
using LiveKit.Proto;
using LiveKit.Rooms.Participants;
using System;
using System.Collections.Generic;

namespace DCL.Multiplayer.Movement.ECS.System
{
    public class RemotePlayersMovementInbox
    {
        public readonly Dictionary<string, Queue<MessageMock>> InboxByParticipantMap = new ();
        private readonly Queue<MessageMock> incomingMessages = new ();
        private readonly IRoomHub room;
        private readonly IMultiplayerSpatialStateSettings settings;
        private readonly bool useMock;

        private MessageMock lastMessage;

        public RemotePlayersMovementInbox(IRoomHub room, IMultiplayerSpatialStateSettings settings)
        {
            this.room = room;
            this.settings = settings;

            room.SceneRoom().DataPipe.DataReceived += OnDataReceived;
            room.IslandRoom().DataPipe.DataReceived += OnDataReceived;
        }

        ~RemotePlayersMovementInbox()
        {
            room.SceneRoom().DataPipe.DataReceived -= OnDataReceived;
            room.IslandRoom().DataPipe.DataReceived -= OnDataReceived;
        }

        private void OnDataReceived(ReadOnlySpan<byte> data, Participant participant, DataPacketKind kind)
        {
            MessageMock? message = MessageMockSerializer.DeserializeMessage(data);

            if (room is not IslandRoomMock)
            {
                if (message != null)
                    Inbox(message, @for: participant.Identity);
            }
            // else
            // {
            //     float sentRate = lastMessage == null ? 1f : message.timestamp - lastMessage.timestamp;
            //     lastMessage = message;
            //
            //     UniTask.Delay(
            //                 TimeSpan.FromSeconds(settings.Latency
            //                                      + (settings.Latency * Random.Range(0, settings.LatencyJitter))
            //                                      + (sentRate * Random.Range(0, settings.PackagesJitter))))
            //            .ContinueWith(() => { Inbox(message, @for: RemotePlayerMovementComponent.SELF_ID); })
            //            .Forget();
            // }
        }

        private void Inbox(MessageMock message, string @for)
        {
            if (InboxByParticipantMap.TryGetValue(@for, out Queue<MessageMock>? queue))
                queue.Enqueue(message);
            else
            {
                var newQueue = new Queue<MessageMock>();
                newQueue.Enqueue(message);

                InboxByParticipantMap.Add(@for, newQueue);
            }
        }
    }
}
