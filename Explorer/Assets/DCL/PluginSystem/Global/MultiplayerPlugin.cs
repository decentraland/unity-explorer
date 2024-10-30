using Arch.SystemGroups;
using CrdtEcsBridge.Components.Transform;
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
using DCL.Multiplayer.Connections.Rooms.Status;
using DCL.Multiplayer.Connections.Systems;
using DCL.Multiplayer.Connections.Systems.RoomIndicator;
using DCL.Multiplayer.Profiles.BroadcastProfiles;
using DCL.Multiplayer.Profiles.Entities;
using DCL.Multiplayer.Profiles.Poses;
using DCL.Multiplayer.Profiles.RemoteAnnouncements;
using DCL.Multiplayer.Profiles.RemoteProfiles;
using DCL.Multiplayer.Profiles.RemoveIntentions;
using DCL.Multiplayer.Profiles.Systems;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Multiplayer.SDK.Components;
using DCL.Multiplayer.SDK.Systems.GlobalWorld;
using DCL.Optimization.Pools;
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
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace DCL.PluginSystem.Global
{
    public class MultiplayerPlugin : IDCLGlobalPlugin<MultiplayerPlugin.Settings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IArchipelagoIslandRoom archipelagoIslandRoom;
        private readonly ICharacterObject characterObject;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly IEmoteStorage emoteStorage;
        private readonly IEntityParticipantTable entityParticipantTable;
        private readonly IGateKeeperSceneRoom gateKeeperSceneRoom;
        private readonly IMessagePipesHub messagePipesHub;
        private readonly IProfileBroadcast profileBroadcast;
        private readonly IProfileRepository profileRepository;
        private readonly ILoadingStatus realFlowLoadingStatus;
        private readonly IRealmData realmData;
        private readonly IRemoteEntities remoteEntities;
        private readonly IRemoteMetadata remoteMetadata;
        private readonly IRoomHub roomHub;
        private readonly RoomsStatus roomsStatus;
        private readonly IScenesCache scenesCache;
        private readonly ICharacterDataPropagationUtility characterDataPropagationUtility;
        private readonly IComponentPoolsRegistry poolsRegistry;

        private IObjectPool<DebugRoomIndicatorView> debugRoomIndicatorPool;

        public MultiplayerPlugin(
            IAssetsProvisioner assetsProvisioner,
            IArchipelagoIslandRoom archipelagoIslandRoom,
            IGateKeeperSceneRoom gateKeeperSceneRoom,
            IRoomHub roomHub,
            RoomsStatus roomsStatus,
            IProfileRepository profileRepository,
            IProfileBroadcast profileBroadcast,
            IDebugContainerBuilder debugContainerBuilder,
            ILoadingStatus realFlowLoadingStatus,
            IEntityParticipantTable entityParticipantTable,
            IMessagePipesHub messagePipesHub,
            IRemoteMetadata remoteMetadata,
            ICharacterObject characterObject,
            IRealmData realmData,
            IRemoteEntities remoteEntities,
            IScenesCache scenesCache,
            IEmoteStorage emoteStorage,
            ICharacterDataPropagationUtility characterDataPropagationUtility,
            IComponentPoolsRegistry poolsRegistry)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.archipelagoIslandRoom = archipelagoIslandRoom;
            this.gateKeeperSceneRoom = gateKeeperSceneRoom;
            this.roomHub = roomHub;
            this.roomsStatus = roomsStatus;
            this.profileRepository = profileRepository;
            this.profileBroadcast = profileBroadcast;
            this.debugContainerBuilder = debugContainerBuilder;
            this.realFlowLoadingStatus = realFlowLoadingStatus;
            this.entityParticipantTable = entityParticipantTable;
            this.messagePipesHub = messagePipesHub;
            this.remoteMetadata = remoteMetadata;
            this.characterObject = characterObject;
            this.remoteEntities = remoteEntities;
            this.realmData = realmData;
            this.scenesCache = scenesCache;
            this.emoteStorage = emoteStorage;
            this.characterDataPropagationUtility = characterDataPropagationUtility;
            this.poolsRegistry = poolsRegistry;
        }

        public void Dispose() { }

        public async UniTask InitializeAsync(Settings settings, CancellationToken ct)
        {
            RemoteAvatarCollider remoteAvatarCollider = (await assetsProvisioner.ProvideMainAssetAsync(settings.RemoteAvatarColliderPrefab, ct)).Value.GetComponent<RemoteAvatarCollider>();
            remoteEntities.Initialize(remoteAvatarCollider);

            await CreateCreateRoomIndicatorPoolAsync(settings, ct);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments globalPluginArguments)
        {
#if !NO_LIVEKIT_MODE
            IFFIClient.Default.EnsureInitialize();

            DebugRoomsSystem.InjectToWorld(ref builder, roomsStatus, archipelagoIslandRoom, gateKeeperSceneRoom, entityParticipantTable, remoteMetadata, debugContainerBuilder,
                roomHub, debugRoomIndicatorPool);

            MultiplayerProfilesSystem.InjectToWorld(ref builder,
                new RemoteAnnouncements(messagePipesHub),
                new LogRemoveIntentions(
                    new ThreadSafeRemoveIntentions(roomHub)
                ),
                new RemoteProfiles(profileRepository, remoteMetadata),
                profileBroadcast,
                remoteEntities,
                remoteMetadata,
                characterObject,
                realFlowLoadingStatus,
                realmData
            );

            ResetDirtyFlagSystem<PlayerCRDTEntity>.InjectToWorld(ref builder);
            PlayerCRDTEntitiesHandlerSystem.InjectToWorld(ref builder, scenesCache);
            PlayerProfileDataPropagationSystem.InjectToWorld(ref builder, characterDataPropagationUtility);
            ResetDirtyFlagSystem<AvatarEmoteCommandComponent>.InjectToWorld(ref builder);
            AvatarEmoteCommandPropagationSystem.InjectToWorld(ref builder, emoteStorage);
            PlayerTransformPropagationSystem.InjectToWorld(ref builder, poolsRegistry.GetReferenceTypePool<SDKTransform>());
#endif
        }

        private async UniTask CreateCreateRoomIndicatorPoolAsync(Settings settings, CancellationToken ct)
        {
            DebugRoomIndicatorView? indicatorPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.DebugRoomIndicator, ct: ct)).Value.GetComponent<DebugRoomIndicatorView>();

            debugRoomIndicatorPool = new GameObjectPool<DebugRoomIndicatorView>(poolsRegistry.RootContainerTransform(),
                creationHandler: () => Object.Instantiate(indicatorPrefab, Vector3.zero, Quaternion.identity), maxSize: PoolConstants.AVATARS_COUNT);
        }

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [SerializeField] public AssetReferenceGameObject RemoteAvatarColliderPrefab;

            public DebugRoomIndicatorViewReference DebugRoomIndicator;

            [Serializable]
            public class DebugRoomIndicatorViewReference : ComponentReference<DebugRoomIndicatorView>
            {
                public DebugRoomIndicatorViewReference(string guid) : base(guid) { }
            }
        }
    }
}
