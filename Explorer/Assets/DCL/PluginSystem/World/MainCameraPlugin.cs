using Arch.SystemGroups;
using Cinemachine;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.ResourcesUnloading;
using DCL.SDKComponents.CameraControl.MainCamera.Systems;
using ECS.LifeCycle;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace DCL.PluginSystem.World
{
    public class MainCameraPlugin : IDCLWorldPlugin<MainCameraPlugin.Settings>
    {
        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: Header(nameof(MainCameraPlugin) + "." + nameof(Settings))]
            [field: Space]
            [field: SerializeField]
            public AssetReferenceGameObject VirtualCameraPrefab;
        }

        private readonly IComponentPoolsRegistry poolsRegistry;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly CacheCleaner cacheCleaner;
        private IComponentPool<CinemachineVirtualCamera>? virtualCameraPoolRegistry;

        public MainCameraPlugin(
            IComponentPoolsRegistry poolsRegistry,
            IAssetsProvisioner assetsProvisioner,
            CacheCleaner cacheCleaner)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.poolsRegistry = poolsRegistry;
            this.cacheCleaner = cacheCleaner;
        }

        public async UniTask InitializeAsync(Settings settings, CancellationToken ct)
        {
            await CreateVirtualCameraPoolAsync(settings, ct);
        }

        private async UniTask CreateVirtualCameraPoolAsync(Settings settings, CancellationToken ct)
        {
            CinemachineVirtualCamera virtualCameraPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.VirtualCameraPrefab, ct: ct)).Value.GetComponent<CinemachineVirtualCamera>();
            virtualCameraPoolRegistry = poolsRegistry.AddGameObjectPool(() => Object.Instantiate(virtualCameraPrefab, Vector3.zero, Quaternion.identity), onRelease: OnPoolRelease, onGet: OnPoolGet);
            cacheCleaner.Register(virtualCameraPoolRegistry);
        }

        private static void OnPoolRelease(CinemachineVirtualCamera virtualCam) =>
            virtualCam.enabled = false;

        private static void OnPoolGet(CinemachineVirtualCamera virtualCam) =>
            virtualCam.enabled = false;

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            finalizeWorldSystems.Add(MainCameraSystem.InjectToWorld(ref builder, virtualCameraPoolRegistry, persistentEntities.Camera, sharedDependencies.EntitiesMap));

            // ResetDirtyFlagSystem<PBMainCamera>.InjectToWorld(ref builder);
            // ResetDirtyFlagSystem<PBVirtualCamera>.InjectToWorld(ref builder);
        }

        public void Dispose()
        {
            virtualCameraPoolRegistry?.Dispose();
        }
    }
}
