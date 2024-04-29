using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character;
using DCL.CharacterTriggerArea.Systems;
using DCL.Optimization.Pools;
using DCL.PluginSystem.Global;
using DCL.PluginSystem.World.Dependencies;
using DCL.ResourcesUnloading;
using DCL.Utilities;
using ECS.LifeCycle;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.PluginSystem.World
{
    public class CharacterTriggerAreaPlugin : IDCLWorldPlugin<CharacterTriggerAreaSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly CacheCleaner cacheCleaner;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy;
        private readonly ICharacterObject characterObject;

        private IComponentPool<CharacterTriggerArea.CharacterTriggerArea>? characterTriggerAreaPoolRegistry;

        public CharacterTriggerAreaPlugin(ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy, ICharacterObject characterObject, IComponentPoolsRegistry poolsRegistry, IAssetsProvisioner assetsProvisioner, CacheCleaner cacheCleaner)
        {
            this.assetsProvisioner = assetsProvisioner;
            componentPoolsRegistry = poolsRegistry;
            this.cacheCleaner = cacheCleaner;
            this.mainPlayerAvatarBaseProxy = mainPlayerAvatarBaseProxy;
            this.characterObject = characterObject;
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
            CharacterTriggerAreaHandlerSystem.InjectToWorld(ref builder, characterTriggerAreaPoolRegistry!, mainPlayerAvatarBaseProxy, sharedDependencies.SceneStateProvider, characterObject);
            CharacterTriggerAreaCleanUpRegisteredCollisionsSystem.InjectToWorld(ref builder);

            var cleanupSystem = CharacterTriggerAreaCleanupSystem.InjectToWorld(ref builder, characterTriggerAreaPoolRegistry!);
            finalizeWorldSystems.Add(cleanupSystem);
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies) { }

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
