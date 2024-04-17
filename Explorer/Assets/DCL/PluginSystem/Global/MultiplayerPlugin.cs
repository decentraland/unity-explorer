using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Character;
using DCL.DebugUtilities;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Connections.FfiClients;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Connections.Systems;
using DCL.Multiplayer.Profiles.BroadcastProfiles;
using DCL.Multiplayer.Profiles.Entities;
using DCL.Multiplayer.Profiles.Poses;
using DCL.Multiplayer.Profiles.RemoteAnnouncements;
using DCL.Multiplayer.Profiles.RemoteProfiles;
using DCL.Multiplayer.Profiles.RemoveIntentions;
using DCL.Multiplayer.Profiles.Systems;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Profiles;
using DCL.UserInAppInitializationFlow;
using ECS;
using LiveKit.Internal.FFIClients;
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
        private readonly IProfileBroadcast profileBroadcast;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly IReadOnlyRealFlowLoadingStatus realFlowLoadingStatus;
        private readonly IEntityParticipantTable entityParticipantTable;
        private readonly IRemoteEntities remoteEntities;
        private readonly IRemotePoses remotePoses;
        private readonly ICharacterObject characterObject;
        private readonly IRealmData realmData;

        public MultiplayerPlugin(
            IArchipelagoIslandRoom archipelagoIslandRoom,
            IGateKeeperSceneRoom gateKeeperSceneRoom,
            IRoomHub roomHub,
            IProfileRepository profileRepository,
            IProfileBroadcast profileBroadcast,
            IDebugContainerBuilder debugContainerBuilder,
            IReadOnlyRealFlowLoadingStatus realFlowLoadingStatus,
            IEntityParticipantTable entityParticipantTable,
            IMessagePipesHub messagePipesHub,
            IRemotePoses remotePoses,
            ICharacterObject characterObject,
            IRealmData realmData,
            IRemoteEntities remoteEntities
        )
        {
            this.archipelagoIslandRoom = archipelagoIslandRoom;
            this.gateKeeperSceneRoom = gateKeeperSceneRoom;
            this.roomHub = roomHub;
            this.profileRepository = profileRepository;
            this.profileBroadcast = profileBroadcast;
            this.debugContainerBuilder = debugContainerBuilder;
            this.realFlowLoadingStatus = realFlowLoadingStatus;
            this.entityParticipantTable = entityParticipantTable;
            this.messagePipesHub = messagePipesHub;
            this.remotePoses = remotePoses;
            this.characterObject = characterObject;
            this.remoteEntities = remoteEntities;
            this.realmData = realmData;
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

            DebugRoomsSystem.InjectToWorld(ref builder, archipelagoIslandRoom, gateKeeperSceneRoom, entityParticipantTable, remotePoses, debugContainerBuilder);
            ConnectionRoomsSystem.InjectToWorld(ref builder, archipelagoIslandRoom, gateKeeperSceneRoom, realFlowLoadingStatus);

            MultiplayerProfilesSystem.InjectToWorld(ref builder,
                new RemoteAnnouncements(messagePipesHub),
                new LogRemoveIntentions(
                    new ThreadSafeRemoveIntentions(roomHub)
                ),
                new RemoteProfiles(profileRepository),
                profileBroadcast,
                remoteEntities,
                remotePoses,
                characterObject,
                realFlowLoadingStatus,
                realmData
            );
#endif
        }
    }
}
