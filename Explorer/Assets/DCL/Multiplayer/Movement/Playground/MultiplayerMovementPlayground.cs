using Arch.Core;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Movement.Systems;
using DCL.Multiplayer.Profiles.Tables;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Playground
{
    public class MultiplayerMovementPlayground : MonoBehaviour
    {
        private MultiplayerMovementMessageBus messageBus;

        private void Awake()
        {
            var world = World.Create();

            messageBus = new MultiplayerMovementMessageBus(new IMessagePipesHub.Fake(), new EntityParticipantTable() ,world);
        }
    }
}
