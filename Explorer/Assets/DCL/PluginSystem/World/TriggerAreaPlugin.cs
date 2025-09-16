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
            // SDKEntityTriggerArea.SDKEntityTriggerArea sdkEntityTriggerAreaPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.TriggerAreaPrefab, ct: ct)).Value.GetComponent<SDKEntityTriggerArea.SDKEntityTriggerArea>();
            // sdkEntityTriggerAreaPoolRegistry = componentPoolsRegistry.AddGameObjectPool(() => Object.Instantiate(sdkEntityTriggerAreaPrefab, Vector3.zero, Quaternion.identity), onRelease: OnTriggerAreaPoolRelease, onGet: OnTriggerAreaPoolGet);
            // cacheCleaner.Register(sdkEntityTriggerAreaPoolRegistry);
        }

        // private static void OnTriggerAreaPoolRelease(SDKEntityTriggerArea.SDKEntityTriggerArea area) =>
        //     area.Dispose();
        //
        // private static void OnTriggerAreaPoolGet(SDKEntityTriggerArea.SDKEntityTriggerArea area) =>
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


