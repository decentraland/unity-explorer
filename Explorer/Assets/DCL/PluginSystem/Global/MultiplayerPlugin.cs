using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Emotes;
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
using DCL.Multiplayer.SDK.Components;
using DCL.Multiplayer.SDK.Systems;
using DCL.Multiplayer.SDK.Systems.GlobalWorld;
using DCL.Profiles;
using DCL.UserInAppInitializationFlow;
using ECS;
using ECS.LifeCycle.Systems;
using ECS.SceneLifeCycle;
using LiveKit.Internal.FFIClients;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class MultiplayerPlugin : IDCLGlobalPlugin<MultiplayerPlugin.Settings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IArchipelagoIslandRoom archipelagoIslandRoom;
        private readonly ICharacterObject characterObject;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly IEmoteCache emoteCache;
        private readonly IEntityParticipantTable entityParticipantTable;
        private readonly IGateKeeperSceneRoom gateKeeperSceneRoom;
        private readonly IMessagePipesHub messagePipesHub;
        private readonly IProfileBroadcast profileBroadcast;
        private readonly IProfileRepository profileRepository;
        private readonly IReadOnlyRealFlowLoadingStatus realFlowLoadingStatus;
        private readonly IRealmData realmData;
        private readonly IRemoteEntities remoteEntities;
        private readonly IRemotePoses remotePoses;
        private readonly IRoomHub roomHub;
        private readonly IScenesCache scenesCache;

        public MultiplayerPlugin(
            IAssetsProvisioner assetsProvisioner,
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
            IRemoteEntities remoteEntities,
            IScenesCache scenesCache,
            IEmoteCache emoteCache
        )
        {
            this.assetsProvisioner = assetsProvisioner;
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
            this.scenesCache = scenesCache;
            this.emoteCache = emoteCache;
        }

        public async UniTask InitializeAsync(Settings settings, CancellationToken ct)
        {
            RemoteAvatarCollider remoteAvatarCollider = (await assetsProvisioner.ProvideMainAssetAsync(settings.RemoteAvatarTransformPrefab, ct)).Value.GetComponent<RemoteAvatarCollider>();
            remoteEntities.Initialize(remoteAvatarCollider);
        }

        public void Dispose() { }

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

            ResetDirtyFlagSystem<PlayerCRDTEntity>.InjectToWorld(ref builder);
            PlayerCRDTEntitiesHandlerSystem.InjectToWorld(ref builder, scenesCache, characterObject);
            PlayerProfileDataPropagationSystem.InjectToWorld(ref builder);
            ResetDirtyFlagSystem<AvatarEmoteCommandComponent>.InjectToWorld(ref builder);
            AvatarEmoteCommandPropagationSystem.InjectToWorld(ref builder, emoteCache);
#endif
        }

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: Header(nameof(MultiplayerPlugin) + "." + nameof(Settings))]
            [field: Space]
            [field: SerializeField]
            public AssetReferenceGameObject RemoteAvatarTransformPrefab;
        }
    }
}
