using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using Decentraland.Kernel.Comms.Rfc4;
using SceneRunner.Scene;
using SceneRuntime.Apis.Modules;
using System;

namespace CrdtEcsBridge.CommunicationsController
{
    public class CommunicationsControllerAPIImplementation : ICommunicationsControllerAPI
    {
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IMessagePipesHub messagePipesHub;

        public CommunicationsControllerAPIImplementation(
            ISceneStateProvider sceneStateProvider,
            IMessagePipesHub messagePipesHub)
        {
            this.sceneStateProvider = sceneStateProvider;
            this.messagePipesHub = messagePipesHub;

            messagePipesHub.IslandPipe().Subscribe<Scene>(Packet.MessageOneofCase.Scene, OnMessageReceived);
            messagePipesHub.ScenePipe().Subscribe<Scene>(Packet.MessageOneofCase.Scene, OnMessageReceived);
        }

        public byte[] SendBinary(byte[] data)
        {
            if (!sceneStateProvider.IsCurrent)
                return Array.Empty<byte>();

            // TODO (Santi): Implement sending of messages
            return Array.Empty<byte>();
        }

        public void Dispose() { }

        private void OnMessageReceived(ReceivedMessage<Scene> receivedMessage)
        {
            // TODO (Santi): Implement reception of messages
        }
    }
}
