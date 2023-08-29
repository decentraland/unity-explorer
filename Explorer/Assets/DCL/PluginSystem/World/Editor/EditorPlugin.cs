using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.PluginSystem.World.Dependencies;
using ECS.Editor;
using ECS.Editor.Systems;
using ECS.LifeCycle;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.PluginSystem.World.Editor
{
    public class EditorPlugin : IDCLWorldPlugin<EditorPlugin.Settings>
    {
        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: Header(nameof(MaterialsPlugin) + "." + nameof(Settings))]
            [field: Space]
            [field: SerializeField]
            public int LoadingAttemptsCount { get; private set; } = 6;

            [field: SerializeField]
            public int PoolInitialCapacity { get; private set; } = 256;

            [field: SerializeField]
            public int PoolMaxSize { get; private set; } = 2048;

            [field: SerializeField]
            public AssetReferenceMaterial basicMaterial;

            [field: SerializeField]
            public AssetReferenceMaterial pbrMaterial;
        }

        private readonly IEcsMonitor sceneMonitor;

        public EditorPlugin()
        {
            this.sceneMonitor = EcsMonitor.Instance;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            var uniqueSceneName = sharedDependencies.SceneData.SceneShortInfo.ToString();
            EcsMonitoringSystem.InjectToWorld(ref builder, uniqueSceneName, this.sceneMonitor);
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies)
        {
            // Do nothing - unless we want to monitor empty scenes too
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public UniTask Initialize(Settings settings, CancellationToken ct) =>
            throw new NotImplementedException();
    }
}
