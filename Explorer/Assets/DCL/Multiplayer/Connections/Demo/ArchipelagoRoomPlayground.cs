using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.Multiplayer.Connections.Archipelago.AdapterAddress.Current;
using DCL.Multiplayer.Connections.Archipelago.LiveConnections;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Connections.Archipelago.SignFlow;
using DCL.Multiplayer.Connections.FfiClients;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.Pools;
using DCL.Multiplayer.Connections.Systems;
using DCL.UserInAppInitializationFlow;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using ECS.Abstract;
using LiveKit.Internal.FFIClients;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;
using System.Net.WebSockets;
using UnityEngine;

namespace DCL.Multiplayer.Connections.Demo
{
    public class ArchipelagoRoomPlayground : MonoBehaviour
    {
        [SerializeField] private LoonCharacterObject loonCharacterObject = new ();

        private BaseUnityLoopSystem system = null!;

        private void Start()
        {
            LaunchAsync().Forget();
        }

        private async UniTaskVoid LaunchAsync()
        {
            IFFIClient.Default.EnsureInitialize();

            var world = World.Create();
            world.Create(new CharacterTransform(new GameObject("Player").transform));

            var memoryPool = new ArrayMemoryPool();

            var multiPool = new LogMultiPool(
                new ThreadSafeMultiPool(),
                Debug.Log
            );

            IArchipelagoSignFlow signFlow = new LiveConnectionArchipelagoSignFlow(
                new LogArchipelagoLiveConnection(
                    new WebSocketArchipelagoLiveConnection(
                        () => new ClientWebSocket(),
                        memoryPool
                    )
                ).WithLog(),
                memoryPool,
                multiPool
            ).WithLog();

            IWeb3IdentityCache? identityCache = await ArchipelagoFakeIdentityCache.NewAsync();

            var archipelagoIslandRoom = new ArchipelagoIslandRoom(
                identityCache,
                signFlow,
                loonCharacterObject,
                ICurrentAdapterAddress.NewDefault(IWebRequestController.DEFAULT, new IRealmData.Fake())
            );
            var realFlowLoadingStatus = new RealFlowLoadingStatus();
            realFlowLoadingStatus.SetStage(RealFlowLoadingStatus.Stage.Completed);
            system = new ConnectionRoomsSystem(world, archipelagoIslandRoom, new IGateKeeperSceneRoom.Fake(), realFlowLoadingStatus);

            while (this)
            {
                system.Update(UnityEngine.Time.deltaTime);
                await UniTask.Yield();
            }
        }
    }
}
