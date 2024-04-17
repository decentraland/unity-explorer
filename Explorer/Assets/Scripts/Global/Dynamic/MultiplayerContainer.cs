using DCL.DebugUtilities;
using DCL.Multiplayer.Connections.Archipelago.AdapterAddress.Current;
using DCL.Multiplayer.Connections.Archipelago.LiveConnections;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Connections.Archipelago.SignFlow;
using DCL.Multiplayer.Connections.GateKeeper.Meta;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Emotes;
using DCL.Multiplayer.Emotes.Interfaces;
using DCL.Multiplayer.Movement;
using DCL.Multiplayer.Movement.Systems;
using DCL.Multiplayer.Profiles.Entities;
using DCL.Multiplayer.Profiles.Poses;
using DCL.Multiplayer.Profiles.Tables;
using DCL.PlacesAPIService;
using DCL.PluginSystem.Global;
using DCL.UserInAppInitializationFlow;
using DCL.Web3.Identities;
using ECS;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;
using System.Buffers;
using System.Net.WebSockets;
using UnityEngine.Pool;
using Utility.PriorityQueue;

namespace Global.Dynamic
{
    public class MultiplayerContainer
    {
        public IRoomHub RoomHub { get; private set; }
        public IMessagePipesHub MessagePipesHub { get; private set; } = null!;
        public EntityParticipantTable EntityParticipantTable { get; private set; } = null!;
        public RemoteEntities RemoteEntities { get; private set; } = null!;
        public IRealmRoomsProvider RealmRoomsProvider { get; private set; } = null!;
        public IEmotesMessageBus EmotesMessageBus { get; private set; } = null!;

        private IGateKeeperSceneRoomProvider gateKeeperSceneRoomProvider = null!;
        private IRemotePoses remotePoses = null!;

        public MultiplayerPlugin CreateMultiplayerPlugin(
            StaticContainer staticContainer,
            DynamicWorldContainer dynamicWorldContainer,
            IRealmData realmData,
            RealFlowLoadingStatus realFlowLoadingStatus,
            DebugContainerBuilder debugBuilder)
        {
            return new MultiplayerPlugin(
                RealmRoomsProvider,
                gateKeeperSceneRoomProvider,
                RoomHub,
                dynamicWorldContainer.ProfileRepository,
                dynamicWorldContainer.ProfileBroadcast,
                debugBuilder,
                realFlowLoadingStatus,
                EntityParticipantTable,
                MessagePipesHub,
                remotePoses,
                staticContainer.CharacterContainer.CharacterObject,
                realmData,
                RemoteEntities
            );
        }

        public MultiplayerMovementPlugin CreateMultiplayerMovementPlugin(StaticContainer staticContainer) =>
            new (staticContainer.AssetsProvisioner, new MultiplayerMovementMessageBus(MessagePipesHub, EntityParticipantTable));

        public void Dispose()
        {
            MessagePipesHub?.Dispose();
        }

        public static MultiplayerContainer Create(StaticContainer staticContainer, IRealmData realmData,
            IWeb3IdentityCache identityCache, IPlacesAPIService placesAPIService)
        {
            var container = new MultiplayerContainer();

            var entityParticipantTable = new EntityParticipantTable();
            var multiPool = new ThreadSafeMultiPool();
            var memoryPool = new ArrayMemoryPool(ArrayPool<byte>.Shared!);
            var metaDataSource = new LogMetaDataSource(new MetaDataSource(realmData, staticContainer.CharacterContainer.CharacterObject, placesAPIService));

            var currentAdapterAddress = ICurrentAdapterAddress.NewDefault(staticContainer.WebRequestsContainer.WebRequestController, realmData);

            container.gateKeeperSceneRoomProvider = new GateKeeperSceneRoomProvider(staticContainer.WebRequestsContainer.WebRequestController, metaDataSource);

            var webSocketArchipelagoLiveConnection =
                new WebSocketArchipelagoLiveConnection(() => new ClientWebSocket(), memoryPool)
                   .WithLog();

            var liveConnectionArchipelagoSignFlow = new LiveConnectionArchipelagoSignFlow(
                webSocketArchipelagoLiveConnection,
                memoryPool,
                multiPool
            ).WithLog();

            container.RealmRoomsProvider = new RealmRoomsProvider(
                identityCache,
                staticContainer.CharacterContainer.CharacterObject,
                staticContainer.WebRequestsContainer.WebRequestController,
                liveConnectionArchipelagoSignFlow,
                currentAdapterAddress);

            container.RoomHub = new RoomHub(container.RealmRoomsProvider, container.gateKeeperSceneRoomProvider);
            container.MessagePipesHub = new MessagePipesHub(container.RoomHub, multiPool, memoryPool);

            var queuePoolFullMovementMessage = new ObjectPool<SimplePriorityQueue<NetworkMovementMessage>>(
                () => new SimplePriorityQueue<NetworkMovementMessage>(),
                actionOnRelease: x => x.Clear()
            );

            var remoteEntities = new RemoteEntities(
                container.RoomHub,
                entityParticipantTable,
                staticContainer.ComponentsContainer.ComponentPoolsRegistry,
                queuePoolFullMovementMessage
            );

            container.remotePoses = new DebounceRemotePoses(
                new RemotePoses(container.RoomHub)
            );

            container.RemoteEntities = remoteEntities;
            container.EntityParticipantTable = entityParticipantTable;
            container.EmotesMessageBus = new MultiplayerEmotesMessageBus(container.MessagePipesHub, entityParticipantTable, identityCache);

            return container;
        }
    }
}
