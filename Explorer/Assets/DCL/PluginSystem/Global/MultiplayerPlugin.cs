using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Connections.FfiClients;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Connections.Systems;
using DCL.Multiplayer.Profiles.BroadcastProfiles;
using DCL.Multiplayer.Profiles.Entities;
using DCL.Multiplayer.Profiles.RemoteAnnouncements;
using DCL.Multiplayer.Profiles.RemoteProfiles;
using DCL.Multiplayer.Profiles.Systems;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Optimization.Pools;
using DCL.Profiles;
using DCL.UserInAppInitializationFlow;
using LiveKit.Internal.FFIClients;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    public class MultiplayerPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly IArchipelagoIslandRoom archipelagoIslandRoom;
        private readonly IGateKeeperSceneRoom gateKeeperSceneRoom;
        private readonly IRoomHub roomHub;
        private readonly IMessagePipesHub messagePipesHub;
        private readonly IProfileRepository profileRepository;
        private readonly IMemoryPool memoryPool;
        private readonly IMultiPool multiPool;
        private readonly IEntityParticipantTable entityParticipantTable;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly RealFlowLoadingStatus realFlowLoadingStatus;

        private IRemoteEntities remoteEntities;

        public MultiplayerPlugin(
            IArchipelagoIslandRoom archipelagoIslandRoom,
            IGateKeeperSceneRoom gateKeeperSceneRoom,
            IRoomHub roomHub,
            IProfileRepository profileRepository,
            IMemoryPool memoryPool,
            IMultiPool multiPool,
            IDebugContainerBuilder debugContainerBuilder,
            RealFlowLoadingStatus realFlowLoadingStatus,
            IEntityParticipantTable entityParticipantTable,
            IComponentPoolsRegistry componentPoolsRegistry,
            IMessagePipesHub messagePipesHub
        )
        {
            this.archipelagoIslandRoom = archipelagoIslandRoom;
            this.gateKeeperSceneRoom = gateKeeperSceneRoom;
            this.roomHub = roomHub;
            this.profileRepository = profileRepository;
            this.memoryPool = memoryPool;
            this.multiPool = multiPool;
            this.debugContainerBuilder = debugContainerBuilder;
            this.realFlowLoadingStatus = realFlowLoadingStatus;
            this.entityParticipantTable = entityParticipantTable;
            this.componentPoolsRegistry = componentPoolsRegistry;
            this.messagePipesHub = messagePipesHub;
        }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct)
        {
            remoteEntities.Initialize();
            return UniTask.CompletedTask;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments _)
        {
#if !NO_LIVEKIT_MODE
            IFFIClient.Default.EnsureInitialize();

            DebugRoomsSystem.InjectToWorld(ref builder, archipelagoIslandRoom, gateKeeperSceneRoom, debugContainerBuilder);
            ConnectionRoomsSystem.InjectToWorld(ref builder, archipelagoIslandRoom, gateKeeperSceneRoom, realFlowLoadingStatus);

            remoteEntities = new RemoteEntities(
                entityParticipantTable,
                componentPoolsRegistry
            );

            MultiplayerProfilesSystem.InjectToWorld(ref builder,
                new ThreadSafeRemoteAnnouncements(messagePipesHub),
                new RemoteProfiles(profileRepository),
                new DebounceProfileBroadcast(
                    new ProfileBroadcast(roomHub, memoryPool, multiPool)
                ),
                remoteEntities
            );
#endif
        }
    }
}
