using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Multiplayer.Connections.Rooms.Connective;
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

        // Additional per-scene rooms (e.g. authoritative Portable Experience scene rooms) keyed by sceneId. When a
        // sceneId has an entry here, its comms are routed to/from that room instead of the host's current scene room.
        private readonly Dictionary<string, RoomChannel> extraRoomsBySceneId = new ();
        private readonly List<string> staleSceneIdsBuffer = new ();

        public SceneCommunicationPipe(IMessagePipesHub messagePipesHub, IGateKeeperSceneRoom sceneRoom)
        {
            this.sceneRoom = sceneRoom;
            messagePipe = messagePipesHub.ScenePipe();
            messagePipe.Subscribe<Scene>(Packet.MessageOneofCase.Scene, InvokeSubscriber, IMessagePipe.ThreadStrict.ORIGIN_THREAD);
        }

        /// <summary>
        ///     Routes the given scene's comms to a dedicated room (its own message pipe) instead of the host's current
        ///     scene room. Idempotent. The caller owns the room and pipe lifecycle; <see cref="RetainOnlyRooms" /> only
        ///     stops routing.
        /// </summary>
        public void RegisterSceneRoom(string sceneId, IConnectiveRoom room, IMessagePipe roomPipe)
        {
            lock (extraRoomsBySceneId)
            {
                if (extraRoomsBySceneId.ContainsKey(sceneId)) return;
                extraRoomsBySceneId[sceneId] = new RoomChannel(room, roomPipe);
            }

            roomPipe.Subscribe<Scene>(Packet.MessageOneofCase.Scene, InvokeSubscriber, IMessagePipe.ThreadStrict.ORIGIN_THREAD);
        }

        /// <summary>
        ///     Stops routing for any registered scene room whose sceneId is no longer present in
        ///     <paramref name="liveSceneIds" />. Disposing the underlying room/pipe is the caller's responsibility.
        /// </summary>
        public void RetainOnlyRooms(ICollection<string> liveSceneIds)
        {
            lock (extraRoomsBySceneId)
            {
                if (extraRoomsBySceneId.Count == 0) return;

                staleSceneIdsBuffer.Clear();

                foreach (string sceneId in extraRoomsBySceneId.Keys)
                    if (!liveSceneIds.Contains(sceneId))
                        staleSceneIdsBuffer.Add(sceneId);

                foreach (string sceneId in staleSceneIdsBuffer)
                    extraRoomsBySceneId.Remove(sceneId);
            }
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

                if (!IsSceneConnected(message.Payload.SceneId)) return;

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
            IMessagePipe pipe;

            lock (extraRoomsBySceneId)
            {
                if (extraRoomsBySceneId.TryGetValue(sceneId, out RoomChannel channel))
                {
                    if (!IsRoomConnected(channel.Room)) return;
                    pipe = channel.Pipe;
                }
                else
                {
                    if (!sceneRoom.IsSceneConnected(sceneId)) return;
                    pipe = messagePipe;
                }
            }

            MessageWrap<Scene> sceneMessage = pipe.NewMessage<Scene>();

            if (!string.IsNullOrEmpty(specialRecipient))
                sceneMessage.AddSpecialRecipient(specialRecipient);

            sceneMessage.Payload.Data = ByteString.CopyFrom(message);
            sceneMessage.Payload.SceneId = sceneId;
            sceneMessage.SendAndDisposeAsync(ct, LKDataPacketKind.KindReliable).Forget();
        }

        private bool IsSceneConnected(string sceneId)
        {
            lock (extraRoomsBySceneId)
                if (extraRoomsBySceneId.TryGetValue(sceneId, out RoomChannel channel))
                    return IsRoomConnected(channel.Room);

            return sceneRoom.IsSceneConnected(sceneId);
        }

        private static bool IsRoomConnected(IConnectiveRoom room) =>
            room.CurrentState() == IConnectiveRoom.State.Running
            && room.Room().Info.ConnectionState == LKConnectionState.ConnConnected;

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

        private readonly struct RoomChannel
        {
            public readonly IConnectiveRoom Room;
            public readonly IMessagePipe Pipe;

            public RoomChannel(IConnectiveRoom room, IMessagePipe pipe)
            {
                Room = room;
                Pipe = pipe;
            }
        }
    }
}
