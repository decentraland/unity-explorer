﻿using DCL.Multiplayer.Connections.Messaging;
using Decentraland.Kernel.Comms.Rfc4;
using System;
using System.Threading;

namespace CrdtEcsBridge.JsModulesImplementation.Communications
{
    public interface ISceneCommunicationPipe
    {
        void SetSceneMessageHandler(Action<SceneMessage> onSceneMessage);

        void RemoveSceneMessageHandler(Action<SceneMessage> onSceneMessage);

        void SendMessage(ReadOnlySpan<byte> message, string sceneId, CancellationToken ct);

        readonly struct SceneMessage
        {
            public readonly ReadOnlyMemory<byte> Data;
            public readonly string SceneId;
            public readonly string FromWalletId;

            private SceneMessage(in ReceivedMessage<Scene> message)
            {
                Data = message.Payload.Data.Memory;
                SceneId = message.Payload.SceneId;
                FromWalletId = message.FromWalletId;
            }

            public static SceneMessage CopyFrom(in ReceivedMessage<Scene> message) =>
                new (message);
        }
    }
}