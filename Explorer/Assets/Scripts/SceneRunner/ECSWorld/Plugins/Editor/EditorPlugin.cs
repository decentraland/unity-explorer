using Arch.Core;
using Arch.SystemGroups;
using Diagnostics.ReportsHandling;
using ECS.Editor;
using ECS.Editor.Systems;
using ECS.LifeCycle;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.AssetBundles.Manifest;
using System.Collections.Generic;
using UnityEngine;

namespace SceneRunner.ECSWorld.Plugins.Editor
{
    public class EditorPlugin : IECSWorldPlugin
    {
        private readonly IEditorSceneMonitor sceneMonitor;

        public EditorPlugin()
        {
            this.sceneMonitor = new EditorSceneMonitor();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            EditorMonitoringSystem.InjectToWorld(ref builder, sharedDependencies.SceneData.SceneShortInfo.ToString(), this.sceneMonitor);
        }
    }
}
