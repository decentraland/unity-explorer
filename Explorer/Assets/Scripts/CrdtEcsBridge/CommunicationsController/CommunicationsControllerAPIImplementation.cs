using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.RoomHubs;
using SceneRunner.Scene;
using SceneRuntime.Apis.Modules;
using System;

namespace CrdtEcsBridge.CommunicationsController
{
    public class CommunicationsControllerAPIImplementation : ICommunicationsControllerAPI
    {
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IMessagePipesHub messagePipesHub;
        private readonly IRoomHub roomHub;

        public CommunicationsControllerAPIImplementation(
            ISceneStateProvider sceneStateProvider,
            IMessagePipesHub messagePipesHub,
            IRoomHub roomHub)
        {
            this.sceneStateProvider = sceneStateProvider;
            this.messagePipesHub = messagePipesHub;
            this.roomHub = roomHub;
        }

        public byte[] SendBinary(byte[] data)
        {
            if (!sceneStateProvider.IsCurrent)
                return Array.Empty<byte>();

            // TODO (Santi): Implement this...
            return Array.Empty<byte>();
        }

        public void Dispose() { }
    }
}
