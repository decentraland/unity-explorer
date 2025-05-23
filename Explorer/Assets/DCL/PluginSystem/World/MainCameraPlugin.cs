using Arch.SystemGroups;
using Cinemachine;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.CharacterCamera;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.ResourcesUnloading;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
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
        private readonly IExposedCameraData cameraData;
        private readonly Arch.Core.World globalWorld;
        private IComponentPool<CinemachineFreeLook>? virtualCameraPoolRegistry;
        private readonly ISceneRestrictionBusController sceneRestrictionBusController;

        public MainCameraPlugin(
            IComponentPoolsRegistry poolsRegistry,
            IAssetsProvisioner assetsProvisioner,
            CacheCleaner cacheCleaner,
            IExposedCameraData cameraData,
            ISceneRestrictionBusController sceneRestrictionBusController,
            Arch.Core.World globalWorld)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.poolsRegistry = poolsRegistry;
            this.cacheCleaner = cacheCleaner;
            this.cameraData = cameraData;
            this.globalWorld = globalWorld;
            this.sceneRestrictionBusController = sceneRestrictionBusController;
        }

        public async UniTask InitializeAsync(Settings settings, CancellationToken ct)
        {
            await CreateVirtualCameraPoolAsync(settings, ct);
        }

        private async UniTask CreateVirtualCameraPoolAsync(Settings settings, CancellationToken ct)
        {
            CinemachineFreeLook virtualCameraPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.VirtualCameraPrefab, ct: ct)).Value.GetComponent<CinemachineFreeLook>();
            virtualCameraPoolRegistry = poolsRegistry.AddGameObjectPool(() => Object.Instantiate(virtualCameraPrefab, Vector3.zero, Quaternion.identity), onRelease: OnPoolRelease, onGet: OnPoolGet);
            cacheCleaner.Register(virtualCameraPoolRegistry);
        }

        private static void OnPoolRelease(CinemachineFreeLook virtualCam) =>
            virtualCam.enabled = false;

        private static void OnPoolGet(CinemachineFreeLook virtualCam) =>
            virtualCam.enabled = false;

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            finalizeWorldSystems.Add(VirtualCameraSystem.InjectToWorld(
                ref builder,
                virtualCameraPoolRegistry,
                sharedDependencies.SceneStateProvider));

            var mainCameraSystem = MainCameraSystem.InjectToWorld(
                ref builder,
                persistentEntities.Camera,
                sharedDependencies.EntitiesMap,
                sharedDependencies.SceneStateProvider,
                cameraData,
                sceneRestrictionBusController,
                globalWorld);
            finalizeWorldSystems.Add(mainCameraSystem);
            sceneIsCurrentListeners.Add(mainCameraSystem);
        }

        public void Dispose()
        {
            virtualCameraPoolRegistry?.Dispose();
        }
    }
}
