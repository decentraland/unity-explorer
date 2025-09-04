/*
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.TriggerArea.Systems;
using ECS.LifeCycle;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.World
{
    public class TriggerAreaPlugin : IDCLWorldPlugin<EntitiesTriggerAreaSettings>
    {
        public void Dispose() { }

        public UniTask InitializeAsync(EntitiesTriggerAreaSettings settings, CancellationToken ct)
        {
            await CreateEntitiesTriggerAreaPoolAsync(settings, ct);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            TriggerAreaHandlerSystem.InjectToWorld(ref builder);
        }

        private async UniTask CreateEntitiesTriggerAreaPoolAsync(EntitiesTriggerAreaSettings settings, CancellationToken ct)
        {
            // CharacterTriggerArea.CharacterTriggerArea characterTriggerAreaPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.CharacterTriggerAreaPrefab, ct: ct)).Value.GetComponent<CharacterTriggerArea.CharacterTriggerArea>();
            // characterTriggerAreaPoolRegistry = componentPoolsRegistry.AddGameObjectPool(() => Object.Instantiate(characterTriggerAreaPrefab, Vector3.zero, Quaternion.identity), onRelease: OnTriggerAreaPoolRelease, onGet: OnTriggerAreaPoolGet);
            // cacheCleaner.Register(characterTriggerAreaPoolRegistry);
        }

        // private static void OnTriggerAreaPoolRelease(CharacterTriggerArea.CharacterTriggerArea area) =>
        //     area.Dispose();
        //
        // private static void OnTriggerAreaPoolGet(CharacterTriggerArea.CharacterTriggerArea area) =>
        //     area.Clear();
    }

    [Serializable]
    public class EntitiesTriggerAreaSettings : IDCLPluginSettings
    {
        [field: Header(nameof(TriggerAreaPlugin) + "." + nameof(EntitiesTriggerAreaSettings))]
        [field: Space]
        [field: SerializeField]
        public AssetReferenceGameObject EntitiesTriggerAreaPrefab;
    }
}
*/


