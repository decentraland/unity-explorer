using Cysharp.Threading.Tasks;
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

namespace DCL.Multiplayer.Connections.Messaging.Pipe
{
    public class MessagePipe : IMessagePipe
    {
        private static readonly ISet<string> SUPPORTED_TYPES = new HashSet<string>
        {
            nameof(Position),
            nameof(AnnounceProfileVersion),
            nameof(ProfileRequest),
            nameof(ProfileResponse),
            nameof(Chat),
            nameof(Scene),
            nameof(Voice),
            nameof(Decentraland.Kernel.Comms.Rfc4.Movement),
        };
        private readonly IDataPipe dataPipe;
        private readonly IMultiPool multiPool;
        private readonly IMemoryPool memoryPool;
        private readonly MessageParser<Packet> messageParser;

        private readonly Dictionary<string, List<Action<(Packet, Participant)>>> subscribers = new ();

        public MessagePipe(IDataPipe dataPipe, IMultiPool multiPool, IMemoryPool memoryPool) : this(
            dataPipe,
            multiPool,
            memoryPool,
            new MessageParser<Packet>(multiPool.Get<Packet>)
        ) { }

        public MessagePipe(IDataPipe dataPipe, IMultiPool multiPool, IMemoryPool memoryPool, MessageParser<Packet> messageParser)
        {
            this.dataPipe = dataPipe;
            this.multiPool = multiPool;
            this.memoryPool = memoryPool;
            this.messageParser = messageParser;

            dataPipe.DataReceived += OnDataReceived;
        }

        ~MessagePipe()
        {
            dataPipe.DataReceived -= OnDataReceived;
        }

        private void OnDataReceived(ReadOnlySpan<byte> data, Participant participant, DataPacketKind kind)
        {
            Packet packet = messageParser.ParseFrom(data).EnsureNotNull("Message is not parsed")!;
            var name = packet.MessageCase.ToString()!;
            NotifySubscribersAsync(name, packet, participant).Forget();
        }

        private async UniTaskVoid NotifySubscribersAsync(string name, Packet packet, Participant participant)
        {
            await UniTask.SwitchToMainThread();
            foreach (Action<(Packet, Participant)>? action in SubscribersList(name))
                action((packet, participant));
        }

        public MessageWrap<T> NewMessage<T>() where T: class, IMessage, new() =>
            new (dataPipe, multiPool, memoryPool);

        public void Subscribe<T>(Action<ReceivedMessage<T>> onMessageReceived) where T: class, IMessage, new()
        {
            const string CURRENT_TYPE = nameof(T);

            if (SUPPORTED_TYPES.Contains(CURRENT_TYPE) == false)
                throw new NotSupportedException($"Type {CURRENT_TYPE} is not supported");

            SubscribersList(CURRENT_TYPE)
               .Add(tuple =>
                    {
                        Packet packet = tuple.Item1!;
                        Participant participant = tuple.Item2!;

                        var receivedMessage = new ReceivedMessage<T>(
                            Payload<T>(packet),
                            packet,
                            participant.Identity,
                            multiPool
                        );

                        onMessageReceived(receivedMessage);
                    }
                );
        }

        private List<Action<(Packet, Participant)>> SubscribersList(string typeName)
        {
            if (subscribers.TryGetValue(typeName, out List<Action<(Packet, Participant)>>? list) == false)
                subscribers[typeName] = list = new List<Action<(Packet, Participant)>>();

            return list!;
        }

        private static T Payload<T>(Packet packet) where T: class =>
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
                Packet.MessageOneofCase.None => throw new ArgumentOutOfRangeException(),
                _ => throw new ArgumentOutOfRangeException(),
            };
    }
}
