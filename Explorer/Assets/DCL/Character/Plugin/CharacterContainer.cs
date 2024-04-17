using Arch.Core;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.Components;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Systems;
using DCL.CharacterMotion.Systems;
using DCL.Multiplayer.Movement;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.PluginSystem.World;
using DCL.PluginSystem.World.Dependencies;
using ECS.LifeCycle;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Character.Plugin
{
    /// <summary>
    ///     Character container is isolated to provide access
    ///     to Character/Player related assets and settings only
    /// </summary>
    public class CharacterContainer : IDCLPlugin<CharacterContainer.Settings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IExposedCameraData exposedCameraData;
        private readonly ExposedTransform exposedTransform;
        private byte bucketPropagationLimit;

        private ProvidedInstance<CharacterObject> characterObject;

        public CharacterContainer(IAssetsProvisioner assetsProvisioner, IExposedCameraData exposedCameraData)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.exposedCameraData = exposedCameraData;

            exposedTransform = new ExposedTransform();
        }

        /// <summary>
        ///     Character Object exists in a single instance
        /// </summary>
        public ICharacterObject CharacterObject => characterObject.Value;

        public void Dispose()
        {
            characterObject.Dispose();
        }

        public async UniTask InitializeAsync(Settings settings, CancellationToken ct)
        {
            characterObject = await assetsProvisioner.ProvideInstanceAsync(
                settings.CharacterObject,
                new Vector3(0f, settings.StartYPosition, 0f),
                Quaternion.identity,
                ct: ct,
                error: nameof(characterObject)
            );

            bucketPropagationLimit = settings.sceneBucketPropagationLimit;
        }

        public WorldPlugin CreateWorldPlugin() =>
            new (exposedTransform, exposedCameraData, bucketPropagationLimit);

        public GlobalPlugin CreateGlobalPlugin() =>
            new (exposedTransform);

        public UniTask InitializeAsync(NoExposedPluginSettings settings, CancellationToken ct) =>
            UniTask.CompletedTask;

        public Entity CreatePlayerEntity(World world) =>
            world.Create(
                new CRDTEntity(SpecialEntitiesID.PLAYER_ENTITY),
                new PlayerComponent(characterObject.Value.CameraFocus),
                new CharacterTransform(characterObject.Value.Transform),
                new PlayerMovementNetworkComponent(characterObject.Value.Controller));

        public class GlobalPlugin : IDCLGlobalPluginWithoutSettings
        {
            private readonly ExposedTransform exposedTransform;

            public GlobalPlugin(ExposedTransform exposedTransform)
            {
                this.exposedTransform = exposedTransform;
            }

            public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
            {
                ExposePlayerTransformSystem.InjectToWorld(ref builder, arguments.PlayerEntity, exposedTransform);
            }
        }

        public class WorldPlugin : IDCLWorldPluginWithoutSettings
        {
            private readonly byte bucketPropagationLimit;
            private readonly IExposedCameraData exposedCameraData;
            private readonly ExposedTransform exposedTransform;

            public WorldPlugin(ExposedTransform exposedTransform, IExposedCameraData exposedCameraData, byte bucketPropagationLimit)
            {
                this.exposedTransform = exposedTransform;
                this.bucketPropagationLimit = bucketPropagationLimit;
                this.exposedCameraData = exposedCameraData;
            }

            public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
            {
                WritePlayerTransformSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter, sharedDependencies.SceneData,
                    exposedTransform, sharedDependencies.ScenePartition, bucketPropagationLimit);

                WriteCameraComponentsSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter, exposedCameraData, sharedDependencies.SceneData,
                    sharedDependencies.ScenePartition, bucketPropagationLimit);
            }

            public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<World> builder, in EmptyScenesWorldSharedDependencies dependencies) { }
        }

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: Header(nameof(CharacterContainer))] [field: Space]
            [field: SerializeField]
            public CharacterObjectRef CharacterObject { get; private set; } = null!;

            [field: SerializeField]
            public float StartYPosition { get; private set; } = 1.0f;

            [field: SerializeField]
            internal byte sceneBucketPropagationLimit { get; private set; } = 3;

            [Serializable]
            public class CharacterObjectRef : ComponentReference<CharacterObject>
            {
                public CharacterObjectRef(string guid) : base(guid) { }
            }
        }
    }
}
