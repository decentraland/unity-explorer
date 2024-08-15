using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.CameraControl.CameraDirector.Systems;
using ECS.LifeCycle;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.World
{
    public class CameraDirectorPlugin : IDCLWorldPlugin<CameraDirectorPlugin.Settings>
    {
        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public async UniTask InitializeAsync(Settings settings, CancellationToken ct)
        {
            await CreateVirtualCameraPoolAsync(settings, ct);
        }

        private async UniTask CreateVirtualCameraPoolAsync(Settings settings, CancellationToken ct)
        {
            // CharacterTriggerArea.CharacterTriggerArea characterTriggerAreaPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.CharacterTriggerAreaPrefab, ct: ct)).Value.GetComponent<CharacterTriggerArea.CharacterTriggerArea>();
            // characterTriggerAreaPoolRegistry = componentPoolsRegistry.AddGameObjectPool(() => Object.Instantiate(characterTriggerAreaPrefab, Vector3.zero, Quaternion.identity), onRelease: OnTriggerAreaPoolRelease, onGet: OnTriggerAreaPoolGet);
            // cacheCleaner.Register(characterTriggerAreaPoolRegistry);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            // ResetDirtyFlagSystem<PBCameraDirector>.InjectToWorld(ref builder);
            // ResetDirtyFlagSystem<PBVirtualCamera>.InjectToWorld(ref builder);

            var cameraDirectorSystem = CameraDirectorSystem.InjectToWorld(ref builder);
            // finalizeWorldSystems.Add(cameraDirectorSystem);
        }

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: Header(nameof(CameraDirectorPlugin) + "." + nameof(Settings))]
            [field: Space]
            [field: SerializeField]
            public AssetReferenceGameObject VirtualCameraPrefab;
        }
    }
}
