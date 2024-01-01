using Arch.SystemGroups;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.SceneUI.Systems;
using ECS.LifeCycle;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace DCL.PluginSystem.World
{
    public class SceneUIPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly UIDocument canvas;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;

        public SceneUIPlugin(
            UIDocument canvas,
            ECSWorldSingletonSharedDependencies singletonSharedDependencies)
        {
            this.canvas = canvas;

            componentPoolsRegistry = singletonSharedDependencies.ComponentPoolsRegistry;
            componentPoolsRegistry.AddComponentPool<Label>();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            UITextHandlerSystem.InjectToWorld(ref builder, canvas, componentPoolsRegistry);
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies)
        {
            UITextHandlerSystem.InjectToWorld(ref builder, canvas, componentPoolsRegistry);
        }
    }
}
