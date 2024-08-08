using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using Decentraland.Kernel.Comms.Rfc4;
using Google.Protobuf;
using LiveKit.Proto;
using System;
using System.Threading;

namespace CrdtEcsBridge.JsModulesImplementation.Communications
{
    /// <summary>
    ///     We can't subscribe to the `Scene` message multiple times
    ///     so Hub handles the subscription and the API implementation handles the message processing
    /// </summary>
    public class CommunicationControllerHub : ICommunicationControllerHub
    {
        private Action<ICommunicationControllerHub.SceneMessage>? onSceneMessage;
        private readonly IMessagePipe messagePipe;

        public CommunicationControllerHub(IMessagePipesHub messagePipesHub)
        {
            messagePipe = messagePipesHub.ScenePipe();
            messagePipe.Subscribe<Scene>(Packet.MessageOneofCase.Scene, InvokeCurrentHandler);
        }

        private void InvokeCurrentHandler(ReceivedMessage<Scene> message)
        {
            using (message)
                onSceneMessage?.Invoke(ICommunicationControllerHub.SceneMessage.CopyFrom(in message));
        }

        public void RemoveSceneMessageHandler(Action<ICommunicationControllerHub.SceneMessage> onSceneMessage)
        {
            lock (this) { this.onSceneMessage -= onSceneMessage; }
        }

        public void SetSceneMessageHandler(Action<ICommunicationControllerHub.SceneMessage> onSceneMessage)
        {
            lock (this) { this.onSceneMessage += onSceneMessage; }
        }

        public void SendMessage(ReadOnlySpan<byte> message, string sceneId, CancellationToken ct)
        {
            MessageWrap<Scene> sceneMessage = messagePipe.NewMessage<Scene>();
            sceneMessage.Payload.Data = ByteString.CopyFrom(message);
            sceneMessage.Payload.SceneId = sceneId;
            sceneMessage.SendAndDisposeAsync(ct, DataPacketKind.KindReliable).Forget();
        }
    }
}
