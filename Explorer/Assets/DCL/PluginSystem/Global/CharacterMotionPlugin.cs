using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.Character;
using DCL.Character.CharacterMotion.Systems;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.CharacterMotion.Systems;
using DCL.DebugUtilities;
using DCL.Optimization.Pools;
using ECS.ComponentsPooling.Systems;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Realm;
using ECS.SceneLifeCycle.Reporting;
using System.Threading;
using UnityEngine;
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

        private CharacterControllerSettings settings;

        public CharacterMotionPlugin(
            ICharacterObject characterObject,
            IDebugContainerBuilder debugContainerBuilder,
            IComponentPoolsRegistry componentPoolsRegistry,
            ISceneReadinessReportQueue sceneReadinessReportQueue,
            ILandscape landscape,
            IScenesCache scenesCache)
        {
            this.characterObject = characterObject;
            this.debugContainerBuilder = debugContainerBuilder;
            this.componentPoolsRegistry = componentPoolsRegistry;
            this.sceneReadinessReportQueue = sceneReadinessReportQueue;
            this.landscape = landscape;
            this.scenesCache = scenesCache;
        }

        public void Dispose()
        {
        }

        public UniTask InitializeAsync(CharacterMotionSettings settings, CancellationToken ct)
        {
            this.settings = settings.controllerSettings;
            return UniTask.CompletedTask;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            Arch.Core.World world = builder.World;

            // Add Motion components
            world.Add(arguments.PlayerEntity,
                new CharacterRigidTransform(),
                (ICharacterControllerSettings)settings,
                characterObject,
                characterObject.Controller,
                new CharacterAnimationComponent(),
                new CharacterEmoteComponent(),
                new CharacterPlatformComponent(),
                new StunComponent(),
                new FeetIKComponent(),
                new HandsIKComponent(),
                new HeadIKComponent { IsEnabled = true });

            InterpolateCharacterSystem.InjectToWorld(ref builder, scenesCache);
            TeleportPositionCalculationSystem.InjectToWorld(ref builder, landscape);
            TeleportCharacterSystem.InjectToWorld(ref builder, sceneReadinessReportQueue);
            RotateCharacterSystem.InjectToWorld(ref builder, scenesCache);
            CalculateCharacterVelocitySystem.InjectToWorld(ref builder, debugContainerBuilder);
            CharacterAnimationSystem.InjectToWorld(ref builder);
            CharacterPlatformSystem.InjectToWorld(ref builder);
            CharacterPlatformUpdateSceneTickSystem.InjectToWorld(ref builder, scenesCache);
            StunCharacterSystem.InjectToWorld(ref builder);
            CalculateCameraFovSystem.InjectToWorld(ref builder);
            FeetIKSystem.InjectToWorld(ref builder, debugContainerBuilder);
            HandsIKSystem.InjectToWorld(ref builder, debugContainerBuilder);
            HeadIKSystem.InjectToWorld(ref builder, debugContainerBuilder, settings);
            ReleasePoolableComponentSystem<Transform, CharacterTransform>.InjectToWorld(ref builder, componentPoolsRegistry);
            SDKAvatarShapesMotionSystem.InjectToWorld(ref builder);
        }
    }
}
