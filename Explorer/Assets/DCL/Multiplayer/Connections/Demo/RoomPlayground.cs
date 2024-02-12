using Arch.Core;
using DCL.Character.Components;
using DCL.Multiplayer.Connections.Credentials.Hub;
using DCL.Multiplayer.Connections.FfiClients;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Pools;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Connections.Systems;
using ECS.Abstract;
using LiveKit.Internal.FFIClients;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;
using UnityEngine;

namespace DCL.Multiplayer.Connections.Demo
{
    public class RoomPlayground : MonoBehaviour
    {
        [SerializeField] private string url = string.Empty;
        [SerializeField] private string authToken = string.Empty;

        private BaseUnityLoopSystem system = null!;

        private void Start()
        {
            var world = World.Create();
            world.Create(new CharacterTransform(new GameObject("Player").transform));

            var credentialsHub = new LogCredentialsHub(
                new ICredentialsHub.Fake(url, authToken),
                Debug.Log
            );

            var multiPool = new LogMultiPool(
                new ThreadSafeMultiPool(),
                Debug.Log
            );

            var messagePipeHub = new MessagePipesHub();

            var roomHub = new LogMutableRoomHub(
                new MessagePipedMutableRoomHub(
                    new MutableRoomHub(multiPool),
                    messagePipeHub,
                    multiPool,
                    new ArrayMemoryPool()
                ),
                Debug.Log
            );

            IFFIClient.Default.EnsureInitialize();
            system = new ConnectionRoomsSystem(world, roomHub, multiPool, credentialsHub);
        }

        private void Update()
        {
            system.Update(Time.deltaTime);
        }
    }
}
