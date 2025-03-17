using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character;
using DCL.CharacterCamera;
using DCL.CharacterTriggerArea.Systems;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.PluginSystem.Global;
using DCL.PluginSystem.World.Dependencies;
using DCL.ResourcesUnloading;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using DCL.SDKComponents.AvatarModifierArea.Systems;
using DCL.SDKComponents.CameraModeArea.Systems;
using DCL.Utilities;
using DCL.Web3.Identities;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.PluginSystem.World
{
    public class CharacterTriggerAreaPlugin : IDCLWorldPlugin<CharacterTriggerAreaSettings>
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

        private IComponentPool<CharacterTriggerArea.CharacterTriggerArea>? characterTriggerAreaPoolRegistry;

        public CharacterTriggerAreaPlugin(
            Arch.Core.World globalWorld,
            ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy,
            ObjectProxy<Entity> cameraEntityProxy,
            ICharacterObject characterObject,
            IComponentPoolsRegistry poolsRegistry,
            IAssetsProvisioner assetsProvisioner,
            CacheCleaner cacheCleaner,
            IExposedCameraData cameraData,
            ISceneRestrictionBusController sceneRestrictionBusController,
            IWeb3IdentityCache web3IdentityCache)
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
        }

        public void Dispose()
        {
            characterTriggerAreaPoolRegistry?.Dispose();
        }

        public async UniTask InitializeAsync(CharacterTriggerAreaSettings settings, CancellationToken ct)
        {
            await CreateCharacterTriggerAreaPoolAsync(settings, ct);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            ResetDirtyFlagSystem<PBCameraModeArea>.InjectToWorld(ref builder);

            CharacterTriggerAreaHandlerSystem.InjectToWorld(ref builder, characterTriggerAreaPoolRegistry!, mainPlayerAvatarBaseProxy, sharedDependencies.SceneStateProvider, characterObject);

            finalizeWorldSystems.Add(AvatarModifierAreaHandlerSystem.InjectToWorld(ref builder, globalWorld, sceneRestrictionBusController, web3IdentityCache));
            finalizeWorldSystems.Add(CameraModeAreaHandlerSystem.InjectToWorld(ref builder, globalWorld, cameraEntityProxy, cameraData, sceneRestrictionBusController));
            finalizeWorldSystems.Add(CharacterTriggerAreaCleanupSystem.InjectToWorld(ref builder, characterTriggerAreaPoolRegistry!));
        }

        private async UniTask CreateCharacterTriggerAreaPoolAsync(CharacterTriggerAreaSettings settings, CancellationToken ct)
        {
            CharacterTriggerArea.CharacterTriggerArea characterTriggerAreaPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.CharacterTriggerAreaPrefab, ct: ct)).Value.GetComponent<CharacterTriggerArea.CharacterTriggerArea>();
            characterTriggerAreaPoolRegistry = componentPoolsRegistry.AddGameObjectPool(() => Object.Instantiate(characterTriggerAreaPrefab, Vector3.zero, Quaternion.identity), onRelease: OnTriggerAreaPoolRelease, onGet: OnTriggerAreaPoolGet);
            cacheCleaner.Register(characterTriggerAreaPoolRegistry);
        }

        private static void OnTriggerAreaPoolRelease(CharacterTriggerArea.CharacterTriggerArea area) =>
            area.Dispose();

        private static void OnTriggerAreaPoolGet(CharacterTriggerArea.CharacterTriggerArea area) =>
            area.Clear();
    }
}
