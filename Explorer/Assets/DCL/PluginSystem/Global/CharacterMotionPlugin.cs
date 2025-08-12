using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Emotes;
using DCL.Character;
using DCL.Character.CharacterMotion.Systems;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.CharacterMotion.Systems;
using DCL.DebugUtilities;
using DCL.Optimization.Pools;
using DCL.Utilities;
using ECS;
using ECS.ComponentsPooling.Systems;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Reporting;
using System.Threading;
using UnityEngine;
using SDKAvatarShapesMotionSystem = DCL.Character.CharacterMotion.Systems.SDKAvatarShapesMotionSystem;

namespace DCL.PluginSystem.Global
{
    public class CharacterMotionPlugin : IDCLGlobalPlugin<CharacterMotionSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly ICharacterObject characterObject;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly ISceneReadinessReportQueue sceneReadinessReportQueue;
        private readonly PlayerParcelTrackerService parcelTrackerService;
        private readonly IScenesCache scenesCache;
        private readonly IRealmData realmData;

        private ProvidedAsset<CharacterControllerSettings> settings;

        public CharacterMotionPlugin(
            IAssetsProvisioner assetsProvisioner,
            ICharacterObject characterObject,
            IDebugContainerBuilder debugContainerBuilder,
            IComponentPoolsRegistry componentPoolsRegistry,
            ISceneReadinessReportQueue sceneReadinessReportQueue,
            PlayerParcelTrackerService parcelTrackerService,
            IScenesCache scenesCache,
            IRealmData realmData)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.characterObject = characterObject;
            this.debugContainerBuilder = debugContainerBuilder;
            this.componentPoolsRegistry = componentPoolsRegistry;
            this.sceneReadinessReportQueue = sceneReadinessReportQueue;
            this.parcelTrackerService = parcelTrackerService;
            this.scenesCache = scenesCache;
            this.realmData = realmData;
        }

        public void Dispose()
        {
            settings.Dispose();
        }

        public async UniTask InitializeAsync(CharacterMotionSettings settings, CancellationToken ct)
        {
            this.settings = await assetsProvisioner.ProvideMainAssetAsync(settings.controllerSettings, ct);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            Arch.Core.World world = builder.World;

            // Add Motion components
            world.Add(arguments.PlayerEntity,
                new CharacterRigidTransform(),
                (ICharacterControllerSettings)settings.Value,
                characterObject,
                characterObject.Controller,
                new CharacterAnimationComponent(),
                new CharacterEmoteComponent(),
                new CharacterPlatformComponent(),
                new StunComponent(),
                new FeetIKComponent(),
                new HandsIKComponent(),
                new HeadIKComponent { IsEnabled = true });

            InterpolateCharacterSystem.InjectToWorld(ref builder);
            TeleportPositionCalculationSystem.InjectToWorld(ref builder);
            TeleportCharacterSystem.InjectToWorld(ref builder, sceneReadinessReportQueue);
            RotateCharacterSystem.InjectToWorld(ref builder);
            CalculateCharacterVelocitySystem.InjectToWorld(ref builder, debugContainerBuilder);
            CharacterAnimationSystem.InjectToWorld(ref builder);
            CharacterPlatformSystem.InjectToWorld(ref builder);
            StunCharacterSystem.InjectToWorld(ref builder);
            CalculateCameraFovSystem.InjectToWorld(ref builder);
            FeetIKSystem.InjectToWorld(ref builder, debugContainerBuilder);
            HandsIKSystem.InjectToWorld(ref builder, debugContainerBuilder);
            HeadIKSystem.InjectToWorld(ref builder, debugContainerBuilder, settings.Value);
            ReleasePoolableComponentSystem<Transform, CharacterTransform>.InjectToWorld(ref builder, componentPoolsRegistry);
            SDKAvatarShapesMotionSystem.InjectToWorld(ref builder);
            PlayerParcelTrackingSystem.InjectToWorld(ref builder, arguments.PlayerEntity, parcelTrackerService, scenesCache, realmData);
        }
    }
}
