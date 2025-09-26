using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character;
using DCL.CharacterCamera;
using DCL.SDKEntityTriggerArea.Systems;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.ResourcesUnloading;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using DCL.SDKComponents.AvatarModifierArea.Systems;
using DCL.SDKComponents.CameraModeArea.Systems;
using DCL.SDKComponents.TriggerArea.Systems;
using DCL.Utilities;
using DCL.Web3.Identities;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace DCL.PluginSystem.World
{
    public class SDKEntityTriggerAreaPlugin : IDCLWorldPlugin<SDKEntityTriggerAreaSettings>
    {
        private readonly Arch.Core.World globalWorld;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly CacheCleaner cacheCleaner;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy;
        private readonly ObjectProxy<Entity> cameraEntityProxy;
        private readonly ICharacterObject characterObject;
        private readonly IExposedCameraData cameraData;
        private readonly ISceneRestrictionBusController sceneRestrictionBusController;
        private readonly IWeb3IdentityCache web3IdentityCache;

        private IComponentPool<SDKEntityTriggerArea.SDKEntityTriggerArea>? sdkEntityTriggerAreaPoolRegistry;
        private IComponentPool<PBTriggerAreaResult.Types.Trigger> triggerAreaResultTriggerPool;

        public SDKEntityTriggerAreaPlugin(
            Arch.Core.World globalWorld,
            ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy,
            ObjectProxy<Entity> cameraEntityProxy,
            ICharacterObject characterObject,
            IComponentPoolsRegistry poolsRegistry,
            IAssetsProvisioner assetsProvisioner,
            CacheCleaner cacheCleaner,
            IExposedCameraData cameraData,
            ISceneRestrictionBusController sceneRestrictionBusController,
            IWeb3IdentityCache web3IdentityCache,
            IComponentPool<PBTriggerAreaResult.Types.Trigger> triggerAreaResultTriggerPool)
        {
            this.globalWorld = globalWorld;
            this.assetsProvisioner = assetsProvisioner;
            componentPoolsRegistry = poolsRegistry;
            this.cacheCleaner = cacheCleaner;
            this.mainPlayerAvatarBaseProxy = mainPlayerAvatarBaseProxy;
            this.cameraEntityProxy = cameraEntityProxy;
            this.characterObject = characterObject;
            this.cameraData = cameraData;
            this.sceneRestrictionBusController = sceneRestrictionBusController;
            this.web3IdentityCache = web3IdentityCache;
            this.triggerAreaResultTriggerPool = triggerAreaResultTriggerPool;
        }

        public void Dispose()
        {
            sdkEntityTriggerAreaPoolRegistry?.Dispose();
        }

        public async UniTask InitializeAsync(SDKEntityTriggerAreaSettings settings, CancellationToken ct)
        {
            await CreateSDKEntityTriggerAreaPoolAsync(settings, ct);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            ResetDirtyFlagSystem<PBCameraModeArea>.InjectToWorld(ref builder);

            sceneIsCurrentListeners.Add(SDKEntityTriggerAreaHandlerSystem.InjectToWorld(ref builder, sdkEntityTriggerAreaPoolRegistry!, mainPlayerAvatarBaseProxy, sharedDependencies.SceneStateProvider, characterObject));

            finalizeWorldSystems.Add(AvatarModifierAreaHandlerSystem.InjectToWorld(ref builder, globalWorld, sceneRestrictionBusController, web3IdentityCache));
            finalizeWorldSystems.Add(CameraModeAreaHandlerSystem.InjectToWorld(ref builder, globalWorld, cameraEntityProxy, cameraData, sceneRestrictionBusController));
            finalizeWorldSystems.Add(TriggerAreaHandlerSystem.InjectToWorld(
                ref builder,
                globalWorld,
                sharedDependencies.EcsToCRDTWriter,
                componentPoolsRegistry.GetReferenceTypePool<PBTriggerAreaResult>(),
                triggerAreaResultTriggerPool,
                sharedDependencies.SceneStateProvider,
                sharedDependencies.EntityCollidersSceneCache,
                sharedDependencies.SceneData));
            finalizeWorldSystems.Add(SDKEntityTriggerAreaCleanupSystem.InjectToWorld(ref builder, sdkEntityTriggerAreaPoolRegistry!));
        }

        private async UniTask CreateSDKEntityTriggerAreaPoolAsync(SDKEntityTriggerAreaSettings settings, CancellationToken ct)
        {
            SDKEntityTriggerArea.SDKEntityTriggerArea triggerAreaPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.TriggerAreaPrefab, ct: ct)).Value.GetComponent<SDKEntityTriggerArea.SDKEntityTriggerArea>();
            sdkEntityTriggerAreaPoolRegistry = componentPoolsRegistry.AddGameObjectPool(() => Object.Instantiate(triggerAreaPrefab, Vector3.zero, Quaternion.identity), onRelease: OnTriggerAreaPoolRelease, onGet: OnTriggerAreaPoolGet);
            cacheCleaner.Register(sdkEntityTriggerAreaPoolRegistry);
        }

        private static void OnTriggerAreaPoolRelease(SDKEntityTriggerArea.SDKEntityTriggerArea area) =>
            area.Dispose();

        private static void OnTriggerAreaPoolGet(SDKEntityTriggerArea.SDKEntityTriggerArea area) =>
            area.Clear();
    }

    [Serializable]
    public class SDKEntityTriggerAreaSettings : IDCLPluginSettings
    {
        [field: Header(nameof(SDKEntityTriggerAreaPlugin) + "." + nameof(SDKEntityTriggerAreaSettings))]
        [field: Space]
        [field: SerializeField]
        public AssetReferenceGameObject TriggerAreaPrefab;
    }
}
