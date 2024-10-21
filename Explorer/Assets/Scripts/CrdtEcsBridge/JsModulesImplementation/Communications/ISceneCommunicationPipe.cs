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

        public delegate void SceneMessageHandler(DecodedMessage message);

        void AddSceneMessageHandler(string sceneId, MsgType msgType, SceneMessageHandler onSceneMessage);

        void RemoveSceneMessageHandler(string sceneId, MsgType msgType, SceneMessageHandler onSceneMessage);

        void SendMessage(ReadOnlySpan<byte> message, string sceneId, CancellationToken ct, string? recipient = null);

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
