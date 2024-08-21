using DCL.Multiplayer.Connections.Messaging;
using Decentraland.Kernel.Comms.Rfc4;
using System;
using System.Threading;

namespace CrdtEcsBridge.JsModulesImplementation.Communications
{
    public interface ICommunicationControllerHub
    {
        void SetSceneMessageHandler(Action<SceneMessage> onSceneMessage);

        void RemoveSceneMessageHandler(Action<SceneMessage> onSceneMessage);

        void SendMessage(ReadOnlySpan<byte> message, string sceneId, CancellationToken ct);

        readonly struct SceneMessage
        {
            public readonly ReadOnlyMemory<byte> Data;
            public readonly string SceneId;
            public readonly string WalletId;

            private SceneMessage(in ReceivedMessage<Scene> message)
            {
                Data = message.Payload.Data.Memory;
                SceneId = message.Payload.SceneId;
                WalletId = message.FromWalletId;
            }

            public static SceneMessage CopyFrom(in ReceivedMessage<Scene> message) =>
                new (message);
        }
    }
}
