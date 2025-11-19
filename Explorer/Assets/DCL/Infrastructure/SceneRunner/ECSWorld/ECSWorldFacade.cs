using Arch.Core;
using Arch.SystemGroups;
using CRDT;
using DCL.Diagnostics;
using DCL.PluginSystem.World;
using ECS.ComponentsPooling.Systems;
using ECS.LifeCycle;
using ECS.Unity.Transforms.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using SystemGroups.Visualiser;
using UnityEngine;
using UnityEngine.Profiling;
using Utility;

namespace SceneRunner.ECSWorld
{
    public readonly struct ECSWorldFacade : IDisposable
    {
        public readonly World EcsWorld;
        public readonly PersistentEntities PersistentEntities;

        private readonly IReadOnlyList<IFinalizeWorldSystem> finalizeWorldSystems;
        private readonly IReadOnlyList<ISceneIsCurrentListener> sceneIsCurrentListeners;

        private readonly SystemGroupWorld systemGroupWorld;

        public ECSWorldFacade(
            SystemGroupWorld systemGroupWorld,
            World ecsWorld,
            PersistentEntities persistentEntities,
            IReadOnlyList<IFinalizeWorldSystem> finalizeWorldSystems,
            IReadOnlyList<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            this.systemGroupWorld = systemGroupWorld;
            EcsWorld = ecsWorld;
            this.finalizeWorldSystems = ReorderFinalizeWorldSystems(finalizeWorldSystems);
            this.sceneIsCurrentListeners = sceneIsCurrentListeners;
            PersistentEntities = persistentEntities;

            return;

            List<IFinalizeWorldSystem> ReorderFinalizeWorldSystems(IReadOnlyList<IFinalizeWorldSystem> systems)
            {
                // Transform plugin has to be the last, because of component release flow.
                // During scene unloading some components are parented to temporary transforms, and if ReleasePoolableComponentSystem
                // is called for transform, before it is for said component, and transform pool has reached its max capacity,
                // transform is marked to deletion by ObjectPool.actionOnDestroy, which disables all components in its
                // children, which has caused AudioSources being disabled.
                return systems
                    .OrderBy(s => s is ReleasePoolableComponentSystem<Transform, TransformComponent> ? 1 : 0)
                    .ToList();
            }
        }

        public void Initialize()
        {
            systemGroupWorld.Initialize();
        }

        public void OnSceneIsCurrentChanged(bool isCurrent)
        {
            for (var i = 0; i < sceneIsCurrentListeners.Count; i++)
            {
                try { sceneIsCurrentListeners[i].OnSceneIsCurrentChanged(isCurrent); }
                catch (Exception e) { ReportHub.LogException(e, ReportCategory.ECS); }
            }
        }

        public void Dispose()
        {
            Query finalizeSDKComponentsQuery = EcsWorld.Query(new QueryDescription().WithAll<CRDTEntity>());

            Profiler.BeginSample("FinalizeSDKComponents");

            for (var i = 0; i < finalizeWorldSystems.Count; i++)
            {
                // We must be able to finalize world no matter what
                try { finalizeWorldSystems[i].FinalizeComponents(in finalizeSDKComponentsQuery); }
                catch (Exception e) { ReportHub.LogException(e, ReportCategory.ECS); }
            }

            Profiler.EndSample();

            SystemGroupSnapshot.Instance.Unregister(systemGroupWorld);

            systemGroupWorld.Dispose();
            EcsWorld.Dispose();
        }
    }
}
