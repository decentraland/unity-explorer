using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.AvatarShape.Assets;
using DCL.AvatarRendering.Emotes;
using DCL.Character;
using DCL.Character.CharacterMotion.Components;
using DCL.Character.CharacterMotion.Systems;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.CharacterMotion.Systems;
using DCL.DebugUtilities;
using DCL.FeatureFlags;
using DCL.Friends;
using DCL.Optimization.Pools;
using DCL.Utilities;
using DCL.Web3.Identities;
using ECS.ComponentsPooling.Systems;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Realm;
using ECS.SceneLifeCycle.Reporting;
using ECS.Unity.GliderProp;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using SDKAvatarShapesMotionSystem = DCL.Character.CharacterMotion.Systems.SDKAvatarShapesMotionSystem;

namespace DCL.PluginSystem.Global
{
    public class CharacterMotionPlugin : IDCLGlobalPlugin<CharacterMotionSettings>
    {
        private readonly ICharacterObject characterObject;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly ISceneReadinessReportQueue sceneReadinessReportQueue;
        private readonly ILandscape landscape;
        private readonly IScenesCache scenesCache;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly ObjectProxy<FriendsCache> friendsCache;

        private CharacterMotionSettings settings;
        private GliderPropView gliderPropPrefab;
        private IObjectPool<PointAtMarkerHolder> pointAtMarkerPool;

        public CharacterMotionPlugin(
            ICharacterObject characterObject,
            IDebugContainerBuilder debugContainerBuilder,
            IComponentPoolsRegistry componentPoolsRegistry,
            ISceneReadinessReportQueue sceneReadinessReportQueue,
            ILandscape landscape,
            IScenesCache scenesCache,
            IAssetsProvisioner assetsProvisioner,
            IWeb3IdentityCache web3IdentityCache,
            ObjectProxy<FriendsCache> friendsCache)
        {
            this.characterObject = characterObject;
            this.debugContainerBuilder = debugContainerBuilder;
            this.componentPoolsRegistry = componentPoolsRegistry;
            this.sceneReadinessReportQueue = sceneReadinessReportQueue;
            this.landscape = landscape;
            this.scenesCache = scenesCache;
            this.assetsProvisioner = assetsProvisioner;
            this.web3IdentityCache = web3IdentityCache;
            this.friendsCache = friendsCache;
        }

        public void Dispose()
        {
        }

        public async UniTask InitializeAsync(CharacterMotionSettings settings, CancellationToken ct)
        {
            this.settings = settings;
            gliderPropPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.Gliding.PropPrefab, ct)).Value;

            PointAtMarkerHolder prefab = (await assetsProvisioner.ProvideMainAssetAsync(
                settings.PointAtMarkerPrefab, ct: ct)).Value.GetComponent<PointAtMarkerHolder>();

            var poolRoot = componentPoolsRegistry.RootContainerTransform();
            var poolParent = new GameObject("POOL_CONTAINER_PointAtMarkers").transform;
            poolParent.parent = poolRoot;

            pointAtMarkerPool = new ObjectPool<PointAtMarkerHolder>(
                createFunc: () => Object.Instantiate(prefab, Vector3.zero, Quaternion.identity, poolParent),
                actionOnRelease: m =>
                {
                    m.ResetState();
                    m.gameObject.SetActive(false);
                },
                actionOnDestroy: UnityObjectUtils.SafeDestroy,
                actionOnGet: m => m.gameObject.SetActive(true));
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            Arch.Core.World world = builder.World;

            // Add Motion components
            world.Add(arguments.PlayerEntity,
                new CharacterRigidTransform(),
                (ICharacterControllerSettings)settings.ControllerSettings,
                characterObject,
                characterObject.Controller,
                new CharacterAnimationComponent(),
                new CharacterEmoteComponent(),
                new CharacterPlatformComponent(),
                new StunComponent(),
                new FeetIKComponent(),
                new HandsIKComponent(),
                new HeadIKComponent(),
                new MovementSpeedLimit(),
                new GlideState(),
                new JumpState(),
                new HandPointAtComponent());

            InterpolateCharacterSystem.InjectToWorld(ref builder, scenesCache);
            TeleportPositionCalculationSystem.InjectToWorld(ref builder, landscape);
            TeleportCharacterSystem.InjectToWorld(ref builder, sceneReadinessReportQueue);
            MovePlayerWithDurationSystem.InjectToWorld(ref builder);
            RotateCharacterSystem.InjectToWorld(ref builder, scenesCache);
            CalculateSpeedLimitSystem.InjectToWorld(ref builder);
            CalculateCharacterVelocitySystem.InjectToWorld(ref builder, debugContainerBuilder);
            CharacterAnimationSystem.InjectToWorld(ref builder);
            CharacterPlatformSystem.InjectToWorld(ref builder);
            CharacterPlatformUpdateSceneTickSystem.InjectToWorld(ref builder, scenesCache);
            StunCharacterSystem.InjectToWorld(ref builder);
            CalculateCameraFovSystem.InjectToWorld(ref builder);
            FeetIKSystem.InjectToWorld(ref builder, debugContainerBuilder);
            HandsIKSystem.InjectToWorld(ref builder, debugContainerBuilder);
            HeadIKSystem.InjectToWorld(ref builder, debugContainerBuilder, settings.ControllerSettings);
            ReleasePoolableComponentSystem<Transform, CharacterTransform>.InjectToWorld(ref builder, componentPoolsRegistry);
            SDKAvatarShapesMotionSystem.InjectToWorld(ref builder);
            GroundDistanceSystem.InjectToWorld(ref builder);
            GliderPropControllerSystem.InjectToWorld(ref builder, settings.Gliding, gliderPropPrefab, componentPoolsRegistry);

            if (!FeaturesRegistry.Instance.IsEnabled(FeatureId.POINT_AT))
                return;

            HandPointAtSystem.InjectToWorld(ref builder, settings.ControllerSettings);
            PointAtMarkerSystem.InjectToWorld(ref builder, pointAtMarkerPool, web3IdentityCache, friendsCache);
            PointAtMarkerCleanUpSystem.InjectToWorld(ref builder, pointAtMarkerPool);
        }
    }
}
