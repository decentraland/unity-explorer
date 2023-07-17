using Arch.Core;
using Arch.SystemGroups;
using ECS.Editor;
using ECS.Editor.Systems;
using ECS.LifeCycle;
using System.Collections.Generic;

namespace SceneRunner.ECSWorld.Plugins.Editor
{
    public class EditorPlugin : IECSWorldPlugin
    {
        private readonly IEditorSceneMonitor sceneMonitor;

        public EditorPlugin()
        {
            this.sceneMonitor = EditorSceneMonitor.Instance;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            var uniqueSceneName = sharedDependencies.SceneData.SceneShortInfo.ToString();
            EditorMonitoringSystem.InjectToWorld(ref builder, uniqueSceneName, this.sceneMonitor);
        }
    }
}
