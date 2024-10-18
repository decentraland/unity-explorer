using DCL.Diagnostics;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using Decentraland.Kernel.Comms.Rfc4;
using Google.Protobuf;
using LiveKit.Proto;
using System;
using System.Collections.Generic;
using System.Threading;

namespace CrdtEcsBridge.JsModulesImplementation.Communications
{
    /// <summary>
    ///     We can't subscribe to the `Scene` message multiple times
    ///     so Hub handles the subscription and the API implementation handles the message processing
    /// </summary>
    public class SceneCommunicationPipe : ISceneCommunicationPipe
    {
        private readonly Dictionary<SubscriberKey, ISceneCommunicationPipe.SceneMessageHandler> sceneMessageHandlers = new ();

        private readonly IGateKeeperSceneRoom sceneRoom;
        private readonly IMessagePipe messagePipe;

        public SceneCommunicationPipe(IMessagePipesHub messagePipesHub, IGateKeeperSceneRoom sceneRoom)
        {
            this.sceneRoom = sceneRoom;
            messagePipe = messagePipesHub.ScenePipe();
            messagePipe.Subscribe<Scene>(Packet.MessageOneofCase.Scene, InvokeSubscriber);
        }

        private void InvokeSubscriber(ReceivedMessage<Scene> message)
        {
            using (message)
            {
                ReadOnlySpan<byte> decodedMessage = message.Payload.Data.Span;
                ISceneCommunicationPipe.MsgType msgType = DecodeMessage(ref decodedMessage);

                if (decodedMessage.Length == 0)
                    return;

                if (!sceneRoom.IsSceneConnected(message.Payload.SceneId)) return;

                SubscriberKey key = new (message.Payload.SceneId, msgType);

                if (sceneMessageHandlers.TryGetValue(key, out ISceneCommunicationPipe.SceneMessageHandler? handler))
                    handler(new ISceneCommunicationPipe.DecodedMessage(decodedMessage, message.FromWalletId));
            }
        }

        private static ISceneCommunicationPipe.MsgType DecodeMessage(ref ReadOnlySpan<byte> value)
        {
            var msgType = (ISceneCommunicationPipe.MsgType)value[0];
            value = value[1..];
            return msgType;
        }

        public void AddSceneMessageHandler(string sceneId, ISceneCommunicationPipe.MsgType msgType, ISceneCommunicationPipe.SceneMessageHandler onSceneMessage)
        {
            SubscriberKey key = new (sceneId, msgType);
            sceneMessageHandlers.Add(key, onSceneMessage);
        }

        public void RemoveSceneMessageHandler(string sceneId, ISceneCommunicationPipe.MsgType msgType, ISceneCommunicationPipe.SceneMessageHandler onSceneMessage)
        {
            SubscriberKey key = new (sceneId, msgType);
            sceneMessageHandlers.Remove(key);
        }

        public void SendMessage(ReadOnlySpan<byte> message, string sceneId, ISceneCommunicationPipe.ConnectivityAssertiveness assertiveness, CancellationToken ct)
        {
            if (!sceneRoom.IsSceneConnected(sceneId))
            {
                if (assertiveness == ISceneCommunicationPipe.ConnectivityAssertiveness.DELIVERY_ASSERTED)
                    ReportHub.LogError(ReportCategory.LIVEKIT, $"Scene \"{sceneId}\" expected to deliver the message but {nameof(GateKeeperSceneRoom)} is connected to \"{sceneRoom.ConnectedScene?.SceneEntityDefinition.id}\"");

                return;
            }

            MessageWrap<Scene> sceneMessage = messagePipe.NewMessage<Scene>();
            sceneMessage.Payload.Data = ByteString.CopyFrom(message);
            sceneMessage.Payload.SceneId = sceneId;
            sceneMessage.SendAndDisposeAsync(ct, DataPacketKind.KindReliable).Forget();
        }

        private readonly struct SubscriberKey : IEquatable<SubscriberKey>
        {
            public readonly string SceneId;
            public readonly ISceneCommunicationPipe.MsgType MsgType;

            public SubscriberKey(string sceneId, ISceneCommunicationPipe.MsgType msgType)
            {
                MsgType = msgType;
                SceneId = sceneId;
            }

            public bool Equals(SubscriberKey other) =>
                SceneId == other.SceneId && MsgType == other.MsgType;

            public override bool Equals(object? obj) =>
                obj is SubscriberKey other && Equals(other);

            public override int GetHashCode() =>
                HashCode.Combine(SceneId, (int)MsgType);
        }
    }
}
