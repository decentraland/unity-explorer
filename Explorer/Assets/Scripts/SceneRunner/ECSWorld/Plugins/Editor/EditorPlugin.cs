using Arch.Core;
using Arch.SystemGroups;
using ECS.Editor;
using ECS.Editor.Systems;
using ECS.LifeCycle;
using SceneRunner.EmptyScene;
using System;
using System.Collections.Generic;

namespace SceneRunner.ECSWorld.Plugins.Editor
{
    public class EditorPlugin : IECSWorldPlugin
    {
        private readonly IEcsMonitor sceneMonitor;

        public EditorPlugin()
        {
            this.sceneMonitor = EcsMonitor.Instance;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            var uniqueSceneName = sharedDependencies.SceneData.SceneShortInfo.ToString();
            EcsMonitoringSystem.InjectToWorld(ref builder, uniqueSceneName, this.sceneMonitor);
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<World> builder, in EmptyScenesWorldSharedDependencies dependencies)
        {
            // Do nothing - unless we want to monitor empty scenes too
        }
    }
}
