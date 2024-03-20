using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using Decentraland.Kernel.Comms.Rfc4;
using Google.Protobuf;
using SceneRunner.Scene;
using SceneRuntime.Apis.Modules;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace CrdtEcsBridge.CommunicationsController
{
    public class CommunicationsControllerAPIImplementation : ICommunicationsControllerAPI
    {
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IMessagePipesHub messagePipesHub;
        private readonly CancellationTokenSource cancellationTokenSource = new ();

        private List<byte[]> eventsToProcess = new ();

        public CommunicationsControllerAPIImplementation(
            ISceneStateProvider sceneStateProvider,
            IMessagePipesHub messagePipesHub)
        {
            this.sceneStateProvider = sceneStateProvider;
            this.messagePipesHub = messagePipesHub;

            messagePipesHub.IslandPipe().Subscribe<Scene>(Packet.MessageOneofCase.Scene, OnMessageReceived);
            messagePipesHub.ScenePipe().Subscribe<Scene>(Packet.MessageOneofCase.Scene, OnMessageReceived);
        }

        public byte[][] SendBinary(byte[][] data)
        {
            if (!sceneStateProvider.IsCurrent)
                return Array.Empty<byte[]>();

            foreach (byte[] message in data)
            {
                SendTo(message, messagePipesHub.IslandPipe());
                SendTo(message, messagePipesHub.ScenePipe());
            }

            byte[][] resultData = eventsToProcess.ToArray();
            eventsToProcess.Clear();
            return resultData;
        }

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }

        private  void SendTo(byte[] message, IMessagePipe messagePipe)
        {
            var sceneMessage = messagePipe.NewMessage<Scene>();
            sceneMessage.Payload.Data = ByteString.CopyFrom(message);
            //sceneMessage.Payload.SceneId = ¿?
            sceneMessage.SendAndDisposeAsync(cancellationTokenSource.Token).Forget();
        }

        private void OnMessageReceived(ReceivedMessage<Scene> receivedMessage)
        {
            if (receivedMessage.Payload.Data.IsEmpty)
                return;

            byte[] senderBytes = Encoding.UTF8.GetBytes(receivedMessage.FromWalletId);
            int messageLength = senderBytes.Length + receivedMessage.Payload.Data.Length + 1;

            var serializedMessage = new byte[messageLength];
            serializedMessage[0] = (byte)senderBytes.Length;
            Array.Copy(senderBytes, 0, serializedMessage, 1, senderBytes.Length);
            Array.Copy(receivedMessage.Payload.Data.ToByteArray(), 0, serializedMessage, senderBytes.Length + 1, receivedMessage.Payload.Data.Length);

            eventsToProcess.Add(serializedMessage);
        }
    }
}
