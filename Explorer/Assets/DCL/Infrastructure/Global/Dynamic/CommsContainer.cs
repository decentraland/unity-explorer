using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.AssetsProvision;
using DCL.DebugUtilities;
using DCL.LiveKit.Public;
using DCL.Multiplayer.Connections.Archipelago.AdapterAddress.Current;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Connections.Archipelago.Rooms.Chat;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.GateKeeper.Meta;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.GateKeeper.Rooms.Options;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Pools;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Connections.Rooms.Connective;
using DCL.Multiplayer.Connections.Rooms.Status;
using DCL.Multiplayer.Connections.Systems.Throughput;
using DCL.Multiplayer.HealthChecks;
using DCL.Multiplayer.Movement;
using DCL.Multiplayer.Profiles.Entities;
using DCL.Multiplayer.Profiles.Poses;
using DCL.Multiplayer.Profiles.RemoteProfiles;
using DCL.Multiplayer.Profiles.Tables;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.PluginSystem.Global;
using DCL.Web3.Identities;
using ECS.SceneLifeCycle.CurrentScene;
using Global.AppArgs;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;
using System;
using System.Buffers;
using UnityEngine;
using UnityEngine.Pool;
using Utility.Ownership;
using Utility.PriorityQueue;
using MultiplayerPlugin = DCL.PluginSystem.Global.MultiplayerPlugin;

namespace Global.Dynamic
{
    /// <summary>
    ///     Realtime communication transports: LiveKit rooms, the room hub, message pipes and the state of remote participants.
    /// </summary>
    public class CommsContainer : IDisposable
    {
        private readonly IArchipelagoIslandRoom archipelagoIslandRoom;
        private readonly IGateKeeperSceneRoom gateKeeperSceneRoom;
        private readonly ChatConnectiveRoom chatRoom;
        private readonly VoiceChatActivatableConnectiveRoom voiceChatRoom;
        private readonly ThroughputBufferBunch islandThroughputBunch;
        private readonly ThroughputBufferBunch sceneThroughputBunch;

        public IRoomHub RoomHub { get; }

        public RoomsStatus RoomsStatus { get; }

        public IMessagePipesHub MessagePipesHub { get; }

        public EntityParticipantTable EntityParticipantTable { get; }

        public MovementInbox MovementInbox { get; }

        public RemoteEntities RemoteEntities { get; }

        public RemoteProfiles RemoteProfiles { get; }

        public IRemoteMetadata RemoteMetadata { get; }

        public IHealthCheck LivekitHealthCheck { get; }

        public CurrentSceneInfo CurrentSceneInfo { get; }

        private CommsContainer(
            IArchipelagoIslandRoom archipelagoIslandRoom,
            IGateKeeperSceneRoom gateKeeperSceneRoom,
            ChatConnectiveRoom chatRoom,
            VoiceChatActivatableConnectiveRoom voiceChatRoom,
            ThroughputBufferBunch islandThroughputBunch,
            ThroughputBufferBunch sceneThroughputBunch,
            IRoomHub roomHub,
            RoomsStatus roomsStatus,
            IMessagePipesHub messagePipesHub,
            EntityParticipantTable entityParticipantTable,
            MovementInbox movementInbox,
            RemoteEntities remoteEntities,
            RemoteProfiles remoteProfiles,
            IRemoteMetadata remoteMetadata,
            IHealthCheck livekitHealthCheck,
            CurrentSceneInfo currentSceneInfo)
        {
            this.archipelagoIslandRoom = archipelagoIslandRoom;
            this.gateKeeperSceneRoom = gateKeeperSceneRoom;
            this.chatRoom = chatRoom;
            this.voiceChatRoom = voiceChatRoom;
            this.islandThroughputBunch = islandThroughputBunch;
            this.sceneThroughputBunch = sceneThroughputBunch;
            RoomHub = roomHub;
            RoomsStatus = roomsStatus;
            MessagePipesHub = messagePipesHub;
            EntityParticipantTable = entityParticipantTable;
            MovementInbox = movementInbox;
            RemoteEntities = remoteEntities;
            RemoteProfiles = remoteProfiles;
            RemoteMetadata = remoteMetadata;
            LivekitHealthCheck = livekitHealthCheck;
            CurrentSceneInfo = currentSceneInfo;
        }

        public static CommsContainer Create(
            StaticContainer staticContainer,
            BootstrapContainer bootstrapContainer,
            IWeb3IdentityCache identityCache,
            World globalWorld,
            IAppArgs appArgs,
            bool isolateScenesCommunication,
            bool enableAnalytics,
            bool localSceneDevelopment)
        {
            var entityParticipantTable = new EntityParticipantTable();
            var movementInbox = new MovementInbox(entityParticipantTable, globalWorld);

            SceneRoomLogMetaDataSource playSceneMetaDataSource = new SceneRoomMetaDataSource(staticContainer.RealmData, staticContainer.CharacterContainer.Transform, globalWorld, isolateScenesCommunication, bootstrapContainer.DecentralandUrlsSource).WithLog();
            SceneRoomLogMetaDataSource localDevelopmentMetaDataSource = new LocalSceneDevelopmentSceneRoomMetaDataSource(staticContainer.WebRequestsContainer.WebRequestController).WithLog();

            var gateKeeperSceneRoomOptions = new GateKeeperSceneRoomOptions(staticContainer.LaunchMode,
                bootstrapContainer.DecentralandUrlsSource,
                playSceneMetaDataSource,
                localDevelopmentMetaDataSource,
                staticContainer.RealmData);

            IGateKeeperSceneRoom gateKeeperSceneRoom = new GateKeeperSceneRoom(staticContainer.WebRequestsContainer.WebRequestController,
                    gateKeeperSceneRoomOptions).AsActivatable();

            var currentAdapterAddress = ICurrentAdapterAddress.NewDefault(staticContainer.RealmData);

            var archipelagoIslandRoom = IArchipelagoIslandRoom.NewDefault(
                identityCache,
                MultiPoolFactory(),
                new ArrayMemoryPool(),
                staticContainer.CharacterContainer.CharacterObject,
                currentAdapterAddress,
                staticContainer.WebRequestsContainer.WebRequestController,
                staticContainer.RealmData
            );

            var chatRoom = new ChatConnectiveRoom(staticContainer.WebRequestsContainer.WebRequestController, URLAddress.FromString(bootstrapContainer.DecentralandUrlsSource.Url(DecentralandUrl.ChatAdapter)));

            var voiceChatRoom = new VoiceChatActivatableConnectiveRoom();

            // LiveKit and Pulse can coexist - there is no harm
            // We can control the messages flow selectively
            IRoomHub roomHub;

            if (appArgs.HasFlag(AppArgsFlags.NO_LIVEKIT_MODE))
            {
                roomHub = NullRoomHub.INSTANCE;
            }
            else
            {
                roomHub = new RoomHub(
                        localSceneDevelopment ? IConnectiveRoom.Null.INSTANCE : archipelagoIslandRoom,
                        gateKeeperSceneRoom,
                        chatRoom,
                        voiceChatRoom
                        );
            }

            var islandThroughputBunch = new ThroughputBufferBunch(new ThroughputBuffer(), new ThroughputBuffer());
            var sceneThroughputBunch = new ThroughputBufferBunch(new ThroughputBuffer(), new ThroughputBuffer());
            var chatThroughputBunch = new ThroughputBufferBunch(new ThroughputBuffer(), new ThroughputBuffer());

            var messagePipesHub = new MessagePipesHub(roomHub, MultiPoolFactory(), new ArrayMemoryPool(ArrayPool<byte>.Shared!), islandThroughputBunch, sceneThroughputBunch, chatThroughputBunch);

            var roomsStatus = new RoomsStatus(
                roomHub,

                //override allowed only in Editor
                Application.isEditor
                    ? new LinkedBox<(bool use, LKConnectionQuality quality)>(
                        () => (bootstrapContainer.DebugSettings.OverrideConnectionQuality, bootstrapContainer.DebugSettings.ConnectionQuality)
                    )
                    : new Box<(bool use, LKConnectionQuality quality)>((false, LKConnectionQuality.QualityExcellent))
            );

            var queuePoolFullMovementMessage = new ObjectPool<SimplePriorityQueue<NetworkMovementMessage, double>>(
                () => new SimplePriorityQueue<NetworkMovementMessage, double>(),
                actionOnRelease: queue => queue.Clear()
            );

            var remoteEntities = new RemoteEntities(
                entityParticipantTable,
                staticContainer.ComponentsContainer.ComponentPoolsRegistry,
                queuePoolFullMovementMessage,
                staticContainer.EntityCollidersGlobalCache,
                movementInbox
            );

            var remoteMetadata = new DebounceRemoteMetadata(new RemoteMetadata(roomHub, staticContainer.RealmData, bootstrapContainer.DecentralandUrlsSource));

            var remoteProfiles = new RemoteProfiles(staticContainer.ProfilesContainer.Repository, remoteMetadata);

            IHealthCheck livekitHealthCheck = bootstrapContainer.DebugSettings.EnableEmulateNoLivekitConnection
                ? new IHealthCheck.AlwaysFails()
                : new StartLiveKitRooms(roomHub);

            livekitHealthCheck = enableAnalytics
                ? livekitHealthCheck.WithFailAnalytics(bootstrapContainer.Analytics.Controller)
                : livekitHealthCheck;

            return new CommsContainer(
                archipelagoIslandRoom,
                gateKeeperSceneRoom,
                chatRoom,
                voiceChatRoom,
                islandThroughputBunch,
                sceneThroughputBunch,
                roomHub,
                roomsStatus,
                messagePipesHub,
                entityParticipantTable,
                movementInbox,
                remoteEntities,
                remoteProfiles,
                remoteMetadata,
                livekitHealthCheck,
                new CurrentSceneInfo());
        }

        public MultiplayerPlugin CreateMultiplayerPlugin(
            StaticContainer staticContainer,
            IAssetsProvisioner assetsProvisioner,
            IDebugContainerBuilder debugBuilder,
            MultiplayerContainer multiplayerContainer) =>
            new (
                assetsProvisioner,
                archipelagoIslandRoom,
                gateKeeperSceneRoom,
                chatRoom,
                RoomHub,
                RoomsStatus,
                RemoteProfiles,
                multiplayerContainer.ProfileBroadcast,
                debugBuilder,
                staticContainer.LoadingStatus,
                EntityParticipantTable,
                RemoteMetadata,
                staticContainer.CharacterContainer.CharacterObject,
                staticContainer.RealmData,
                RemoteEntities,
                staticContainer.ScenesCache,
                staticContainer.EmoteStorage,
                staticContainer.CharacterDataPropagationUtility,
                staticContainer.ComponentsContainer.ComponentPoolsRegistry,
                islandThroughputBunch,
                sceneThroughputBunch,
                voiceChatRoom,
                // TODO: properly branch profile announcements depending on server setup
                multiplayerContainer.RemoteAnnouncements,
                multiplayerContainer.RemoveIntentions,
                MovementInbox
            );

        public ConnectionStatusPanelPlugin CreateConnectionStatusPanelPlugin(IAssetsProvisioner assetsProvisioner, IAppArgs appArgs) =>
            new (RoomsStatus, CurrentSceneInfo, assetsProvisioner, appArgs);

        public void Dispose()
        {
            MessagePipesHub.Dispose();
        }

        private static IMultiPool MultiPoolFactory() =>
            new DCLMultiPool();
    }
}
