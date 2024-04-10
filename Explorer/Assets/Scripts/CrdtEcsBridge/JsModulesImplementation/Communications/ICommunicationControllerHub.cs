using DCL.Multiplayer.Connections.Messaging;
using Decentraland.Kernel.Comms.Rfc4;
using System;
using System.Threading;

namespace CrdtEcsBridge.JsModulesImplementation.Communications
{
    public interface ICommunicationControllerHub
    {
        void SetSceneMessageHandler(Action<ReceivedMessage<Scene>> onSceneMessage);

        void SendMessage(ReadOnlySpan<byte> message, string sceneId, CancellationToken ct);
    }
}
