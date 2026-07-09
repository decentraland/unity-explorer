using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Multiplayer.Connections.Rooms.Connective;
using System;
using System.Threading;

namespace CrdtEcsBridge.JsModulesImplementation.Communications
{
    public interface ISceneCommunicationPipe
    {
        public enum MsgType
        {
            String = 1, // SDK scenes MessageBus messages
            Uint8Array = 2,
            CommsData = 3, // CommsApi publish/subscribe topic-based data
        }

        public enum ConnectivityAssertiveness
        {
            /// <summary>
            ///     Message will be dropped silently if the scene is not connected
            /// </summary>
            DROP_IF_NOT_CONNECTED = 0,

            /// <summary>
            ///     Additional information will be printed if the scene is not connected
            /// </summary>
            DELIVERY_ASSERTED = 1,
        }

        public delegate void SceneMessageHandler(DecodedMessage message);

        void AddSceneMessageHandler(string sceneId, MsgType msgType, SceneMessageHandler onSceneMessage);

        void RemoveSceneMessageHandler(string sceneId, MsgType msgType, SceneMessageHandler onSceneMessage);

        /// <summary>
        ///     Routes the given scene's comms to a dedicated room (its own message pipe) instead of the host's
        ///     current scene room. Idempotent. The caller owns the room and pipe lifecycle; <see cref="RemoveSceneRoom" />
        ///     only stops routing and removes this pipe's inbound subscription.
        /// </summary>
        void RegisterSceneRoom(string sceneId, IConnectiveRoom room, IMessagePipe roomPipe);

        /// <summary>
        ///     Stops routing for a previously registered scene room and unsubscribes from its inbound pipe. Disposing
        ///     the underlying room/pipe is the caller's responsibility. No-op if the scene was never registered.
        /// </summary>
        void RemoveSceneRoom(string sceneId);

        void SendMessage(ReadOnlySpan<byte> message, string sceneId, ConnectivityAssertiveness assertiveness, CancellationToken ct, string? specialRecipient = null);

        readonly ref struct DecodedMessage
        {
            /// <summary>
            ///     Data without message type
            /// </summary>
            public readonly ReadOnlySpan<byte> Data;
            public readonly string FromWalletId;
            public readonly bool IsTrustedSource;

            public DecodedMessage(ReadOnlySpan<byte> data, string fromWalletId, bool isTrustedSource)
            {
                Data = data;
                FromWalletId = fromWalletId;
                IsTrustedSource = isTrustedSource;
            }
        }
    }
}
