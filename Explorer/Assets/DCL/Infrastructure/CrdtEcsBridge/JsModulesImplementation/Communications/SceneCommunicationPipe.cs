using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using Decentraland.Kernel.Comms.Rfc4;
using Google.Protobuf;
using DCL.LiveKit.Public;
using LiveKit.Proto;
using System;
using System.Collections.Generic;
using System.Threading;
using DCL.SceneBannedUsers;

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
            messagePipe.Subscribe<Scene>(Packet.MessageOneofCase.Scene, InvokeSubscriber, IMessagePipe.ThreadStrict.ORIGIN_THREAD);
        }

        private void InvokeSubscriber(ReceivedMessage<Scene> message)
        {
            using (message)
            {
                ReadOnlySpan<byte> decodedMessage = message.Payload.Data.Span;
                ISceneCommunicationPipe.MsgType msgType = DecodeMessage(ref decodedMessage);
                bool isTrustedSource = IsTrustedSource(message.FromWalletId);

                if (decodedMessage.Length == 0)
                    return;

                // TODO: If the room is connected but the scene is not connected **yet** the message will be skipped and forgotten

                if (!sceneRoom.IsSceneConnected(message.Payload.SceneId)) return;

                SubscriberKey key = new (message.Payload.SceneId, msgType);

                ISceneCommunicationPipe.SceneMessageHandler handler;

                lock (sceneMessageHandlers)
                    if (!sceneMessageHandlers.TryGetValue(key, out handler))
                        return;

                ISceneCommunicationPipe.DecodedMessage dm = new (
                    decodedMessage,
                    message.FromWalletId,
                    isTrustedSource
                );

                handler(dm);
            }
        }

        private bool IsTrustedSource(string walletId)
        {
            SceneAdminResult result = RoomMetadataCurrentScene.Instance.IsAdmin(walletId);

            return result.Match(
                    onSuccess: () => true, // Message is considered safe if it's from a scene admin
                    onLocalSceneDevelopment: () => true, //sceneAdmins are not applicable in cases like LSD
                    onNotAdmin: () => false,
                    onNotLoadedYet: () => false // Consider the user as non-admin until we know for sure
                    );
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

            lock (sceneMessageHandlers)
            {
                // See: https://github.com/decentraland/unity-explorer/issues/8183
                sceneMessageHandlers[key] = onSceneMessage;
            }
        }

        public void RemoveSceneMessageHandler(string sceneId, ISceneCommunicationPipe.MsgType msgType, ISceneCommunicationPipe.SceneMessageHandler onSceneMessage)
        {
            SubscriberKey key = new (sceneId, msgType);

            lock (sceneMessageHandlers)
            {
                // Since message handlers might be replaced, we need to check that the removal of the key belongs to the handler
                if (sceneMessageHandlers.TryGetValue(key, out var current) && current == onSceneMessage)
                    sceneMessageHandlers.Remove(key);
            }
        }

        public void SendMessage(ReadOnlySpan<byte> message, string sceneId, ISceneCommunicationPipe.ConnectivityAssertiveness assertiveness, CancellationToken ct, string? specialRecipient = null)
        {
            if (!sceneRoom.IsSceneConnected(sceneId)) return;

            MessageWrap<Scene> sceneMessage = messagePipe.NewMessage<Scene>();

            if (!string.IsNullOrEmpty(specialRecipient))
                sceneMessage.AddSpecialRecipient(specialRecipient);

            sceneMessage.Payload.Data = ByteString.CopyFrom(message);
            sceneMessage.Payload.SceneId = sceneId;
            sceneMessage.SendAndDisposeAsync(ct, LKDataPacketKind.KindReliable).Forget();
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
