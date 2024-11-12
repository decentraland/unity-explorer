using DCL.Multiplayer.Connections.Messaging;
using Decentraland.Kernel.Comms.Rfc4;
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

        void SendMessage(ReadOnlySpan<byte> message, string sceneId, ConnectivityAssertiveness assertiveness, CancellationToken ct);

        readonly ref struct DecodedMessage
        {
            /// <summary>
            ///     Data without message type
            /// </summary>
            public readonly ReadOnlySpan<byte> Data;
            public readonly string FromWalletId;

            public DecodedMessage(ReadOnlySpan<byte> data, string fromWalletId)
            {
                Data = data;
                FromWalletId = fromWalletId;
            }
        }
    }
}
