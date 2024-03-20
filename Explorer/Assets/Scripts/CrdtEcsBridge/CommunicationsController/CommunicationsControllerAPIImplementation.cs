using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using Decentraland.Kernel.Comms.Rfc4;
using Google.Protobuf;
using LiveKit.Proto;
using SceneRunner.Scene;
using SceneRuntime.Apis.Modules;
using System.Threading;

namespace CrdtEcsBridge.CommunicationsController
{
    public class CommunicationsControllerAPIImplementation : ICommunicationsControllerAPI
    {
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IMessagePipesHub messagePipesHub;
        private readonly CancellationTokenSource cancellationTokenSource = new ();

        public CommunicationsControllerAPIImplementation(
            ISceneStateProvider sceneStateProvider,
            IMessagePipesHub messagePipesHub)
        {
            this.sceneStateProvider = sceneStateProvider;
            this.messagePipesHub = messagePipesHub;

            messagePipesHub.IslandPipe().Subscribe<Scene>(Packet.MessageOneofCase.Scene, OnMessageReceived);
            messagePipesHub.ScenePipe().Subscribe<Scene>(Packet.MessageOneofCase.Scene, OnMessageReceived);
        }

        public ByteString SendBinary(ByteString data)
        {
            if (!sceneStateProvider.IsCurrent)
                return ByteString.Empty;

            SendTo(data, messagePipesHub.IslandPipe());
            SendTo(data, messagePipesHub.ScenePipe());

            return data;
        }

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }

        private  void SendTo(ByteString message, IMessagePipe messagePipe)
        {
            var sceneMessage = messagePipe.NewMessage<Scene>();
            sceneMessage.Payload.Data = message;
            //sceneMessage.Payload.SceneId = ¿?
            sceneMessage.SendAndDisposeAsync(cancellationTokenSource.Token, DataPacketKind.KindReliable).Forget();
        }

        private void OnMessageReceived(ReceivedMessage<Scene> receivedMessage)
        {
            // TODO (Santi): Implement reception of messages
        }
    }
}
