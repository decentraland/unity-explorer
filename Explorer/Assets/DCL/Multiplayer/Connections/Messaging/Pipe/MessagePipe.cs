using CrdtEcsBridge.Components;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Connections.Typing;
using DCL.Utilities.Extensions;
using Decentraland.Kernel.Comms.Rfc4;
using Google.Protobuf;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;
using LiveKit.Proto;
using LiveKit.Rooms.DataPipes;
using LiveKit.Rooms.Participants;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;

namespace DCL.Multiplayer.Connections.Messaging.Pipe
{
    public class MessagePipe : IMessagePipe
    {
        private readonly IDataPipe dataPipe;
        private readonly IMultiPool sendingMultiPool;
        private readonly IMemoryPool memoryPool;
        private readonly MessageParser<Packet> messageParser;
        private readonly uint supportedVersion;
        private readonly RoomSource roomId;
        private readonly CancellationTokenSource cts;

        private readonly Dictionary<Packet.MessageOneofCase, (List<Action<(Packet, Participant, string)>> list, IMessagePipe.ThreadStrict strict)> subscribers = new ();

        private bool isDisposed;

        public MessagePipe(IDataPipe dataPipe, IMultiPool sendingMultiPool, IMultiPool receivingMultiPool, IMemoryPool memoryPool, RoomSource roomId) : this(
            dataPipe,
            sendingMultiPool,
            memoryPool,
            new MessageParser<Packet>(() =>
            {
                Packet packet = receivingMultiPool.Get<Packet>();
                packet.ClearProtobufComponent();
                return packet;
            }),
            100, roomId) { }

        public MessagePipe(IDataPipe dataPipe, IMultiPool sendingMultiPool, IMemoryPool memoryPool, MessageParser<Packet> messageParser, uint supportedVersion,
            RoomSource roomId)
        {
            this.dataPipe = dataPipe;
            this.sendingMultiPool = sendingMultiPool;
            this.memoryPool = memoryPool;
            this.messageParser = messageParser;
            this.supportedVersion = supportedVersion;
            this.roomId = roomId;

            cts = new CancellationTokenSource();
            dataPipe.DataReceived += OnDataReceived;
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                ReportHub.LogError(ReportCategory.LIVEKIT, "Trying to dispose twice?");
                return;
            }

            dataPipe.DataReceived -= OnDataReceived;
            cts.SafeCancelAndDispose();
            isDisposed = true;
        }

        private void OnDataReceived(ReadOnlySpan<byte> data, Participant participant, string topic, DataPacketKind kind)
        {
            try
            {
                Packet packet = messageParser.ParseFrom(data).EnsureNotNull("Message is not parsed")!;
                var name = packet.MessageCase;
                NotifySubscribersAsync(name, packet, participant, topic, cts.Token).Forget();
            }
            catch (Exception e)
            {
                ReportHub.LogError(
                    ReportCategory.LIVEKIT,
                    $"Invalid data arrived from participant {participant.Identity}: {e.Message} : {data.HexReadableString()}"
                );
            }
        }

        private async UniTaskVoid NotifySubscribersAsync(Packet.MessageOneofCase name, Packet packet, Participant participant, string topic, CancellationToken ctsToken)
        {
            try
            {
                (List<Action<(Packet, Participant)>> list, IMessagePipe.ThreadStrict strict)? receiver = SubscribersListOrNull(name);

                if (receiver.HasValue == false)
                    return;

                (List<Action<(Packet, Participant)>> list, IMessagePipe.ThreadStrict strict) r = receiver.Value;

                if (r.strict is IMessagePipe.ThreadStrict.MAIN_THREAD_ONLY)
                    await UniTask.SwitchToMainThread();

                foreach (Action<(Packet, Participant, string)>? action in r.list)
                {
                    ctsToken.ThrowIfCancellationRequested();
                    action((packet, participant, topic));
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.LIVEKIT); }
        }

        public MessageWrap<T> NewMessage<T>(string topic) where T: class, IMessage, new() =>
            new (dataPipe, sendingMultiPool, memoryPool, topic, supportedVersion);

        public void Subscribe<T>(Packet.MessageOneofCase ofCase, Action<ReceivedMessage<T>> onMessageReceived, IMessagePipe.ThreadStrict threadStrict) where T: class, IMessage, new()
        {
            (List<Action<(Packet, Participant)>> list, IMessagePipe.ThreadStrict strict) item = SubscribersList(ofCase, threadStrict);

            if (item.list.Count > 0)
                throw new InvalidOperationException($"Only single subscriber per type is allowed. Type: {ofCase}");

            item.list
               .Add(tuple =>
                    {
                        Packet packet = tuple.Item1!;
                        Participant participant = tuple.Item2!;
                        string topic = tuple.Item3!;

                         uint version = packet.ProtocolVersion;

                         if (version != supportedVersion)
                         {
                             ReportHub.LogWarning(
                                 ReportCategory.LIVEKIT,
                                 $"Received message with unsupported version {version} from {participant.Identity} with type {packet.MessageCase}"
                             );

                             return;
                         }

                         T? payload = Payload<T>(packet);

                         if (payload == null)
                         {
                             ReportHub.LogError(
                                 ReportCategory.LIVEKIT,
                                 $"Received invalid message from {participant.Identity} with type {packet.MessageCase}"
                             );

                             return;
                         }

                        var receivedMessage = new ReceivedMessage<T>(
                            payload,
                            packet,
                            participant.Identity,
                            sendingMultiPool,
                            roomId,
                            topic
                        );

                         onMessageReceived(receivedMessage);
                     }
                 );
        }

        private (List<Action<(Packet, Participant, string)>> list, IMessagePipe.ThreadStrict strict) SubscribersList(Packet.MessageOneofCase typeName, IMessagePipe.ThreadStrict threadStrict)
        {
            if (subscribers.TryGetValue(typeName, out (List<Action<(Packet, Participant, string)>> list, IMessagePipe.ThreadStrict strict) item) == false)
                subscribers[typeName] = item = (new List<Action<(Packet, Participant, string)>>(), threadStrict);

            return item;
        }

        private (List<Action<(Packet, Participant, string)>> list, IMessagePipe.ThreadStrict strict)? SubscribersListOrNull(Packet.MessageOneofCase typeName)
        {
            if (subscribers.TryGetValue(typeName, out (List<Action<(Packet, Participant, string)>> list, IMessagePipe.ThreadStrict strict) item) == false)
                return null;

            return item;
        }

        private static T? Payload<T>(Packet packet) where T: class =>
            packet.MessageCase switch
            {
                Packet.MessageOneofCase.Position => (packet.Position as T).EnsureNotNull(),
                Packet.MessageOneofCase.ProfileVersion => (packet.ProfileVersion as T).EnsureNotNull(),
                Packet.MessageOneofCase.ProfileRequest => (packet.ProfileRequest as T).EnsureNotNull(),
                Packet.MessageOneofCase.ProfileResponse => (packet.ProfileResponse as T).EnsureNotNull(),
                Packet.MessageOneofCase.Chat => (packet.Chat as T).EnsureNotNull(),
                Packet.MessageOneofCase.Scene => (packet.Scene as T).EnsureNotNull(),
                Packet.MessageOneofCase.Voice => (packet.Voice as T).EnsureNotNull(),
                Packet.MessageOneofCase.Movement => (packet.Movement as T).EnsureNotNull(),
                Packet.MessageOneofCase.MovementCompressed => (packet.MovementCompressed as T).EnsureNotNull(),
                Packet.MessageOneofCase.PlayerEmote => (packet.PlayerEmote as T).EnsureNotNull(),
                Packet.MessageOneofCase.SceneEmote => (packet.SceneEmote as T).EnsureNotNull(),
                Packet.MessageOneofCase.None => null,
                _ => null,
            };
    }
}
