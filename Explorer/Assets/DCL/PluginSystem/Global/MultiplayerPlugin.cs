using Arch.SystemGroups;
using DCL.DebugUtilities;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Connections.FfiClients;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Connections.Systems;
using DCL.Multiplayer.Profiles.BroadcastProfiles;
using DCL.Multiplayer.Profiles.Entities;
using DCL.Multiplayer.Profiles.RemoteAnnouncements;
using DCL.Multiplayer.Profiles.RemoteProfiles;
using DCL.Multiplayer.Profiles.Systems;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Profiles;
using DCL.UserInAppInitializationFlow;
using LiveKit.Internal.FFIClients;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;

namespace DCL.PluginSystem.Global
{
    public class MultiplayerPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly IArchipelagoIslandRoom archipelagoIslandRoom;
        private readonly IGateKeeperSceneRoom gateKeeperSceneRoom;
        private readonly IProfileRepository profileRepository;
        private readonly IMemoryPool memoryPool;
        private readonly IMultiPool multiPool;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly RealFlowLoadingStatus realFlowLoadingStatus;

        public MultiplayerPlugin(IArchipelagoIslandRoom archipelagoIslandRoom, IGateKeeperSceneRoom gateKeeperSceneRoom, IProfileRepository profileRepository, IMemoryPool memoryPool, IMultiPool multiPool,
            IDebugContainerBuilder debugContainerBuilder, RealFlowLoadingStatus realFlowLoadingStatus)
        {
            this.archipelagoIslandRoom = archipelagoIslandRoom;
            this.gateKeeperSceneRoom = gateKeeperSceneRoom;
            this.profileRepository = profileRepository;
            this.memoryPool = memoryPool;
            this.multiPool = multiPool;
            this.debugContainerBuilder = debugContainerBuilder;
            this.realFlowLoadingStatus = realFlowLoadingStatus;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments _)
        {
#if !NO_LIVEKIT_MODE
            IFFIClient.Default.EnsureInitialize();

            var roomHub = new RoomHub(archipelagoIslandRoom, gateKeeperSceneRoom);

            DebugRoomsSystem.InjectToWorld(ref builder, archipelagoIslandRoom, gateKeeperSceneRoom, debugContainerBuilder);
            ConnectionRoomsSystem.InjectToWorld(ref builder, archipelagoIslandRoom, gateKeeperSceneRoom, realFlowLoadingStatus);

            MultiplayerProfilesSystem.InjectToWorld(ref builder,
                new ThreadSafeRemoteAnnouncements(roomHub, multiPool),
                new RemoteProfiles(profileRepository),
                new DebounceProfileBroadcast(
                    new ProfileBroadcast(roomHub, memoryPool, multiPool)
                ),
                new RemoteEntities(
                    new EntityParticipantTable()
                )
            );
#endif
        }
    }
}
