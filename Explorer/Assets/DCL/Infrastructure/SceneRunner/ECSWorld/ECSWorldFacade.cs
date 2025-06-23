using Arch.Core;
using Arch.SystemGroups;
using CRDT;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using DCL.PluginSystem.World;
using ECS.LifeCycle;
using System;
using System.Collections.Generic;
using UnityEngine.Profiling;
using SystemGroups.Visualiser;

namespace SceneRunner.ECSWorld
{
    public readonly struct ECSWorldFacade : IUniTaskAsyncDisposable
    {
        public readonly World EcsWorld;
        public readonly PersistentEntities PersistentEntities;

        private readonly IReadOnlyList<IFinalizeWorldSystem> finalizeWorldSystems;
        private readonly IReadOnlyList<ISceneIsCurrentListener> sceneIsCurrentListeners;
        private readonly IPerformanceBudget performanceBudget;

        private readonly SystemGroupWorld systemGroupWorld;

        public ECSWorldFacade(
            SystemGroupWorld systemGroupWorld,
            World ecsWorld,
            PersistentEntities persistentEntities,
            IReadOnlyList<IFinalizeWorldSystem> finalizeWorldSystems,
            IReadOnlyList<ISceneIsCurrentListener> sceneIsCurrentListeners,
            IPerformanceBudget performanceBudget)
        {
            this.systemGroupWorld = systemGroupWorld;
            EcsWorld = ecsWorld;
            this.finalizeWorldSystems = finalizeWorldSystems;
            this.sceneIsCurrentListeners = sceneIsCurrentListeners;
            this.performanceBudget = performanceBudget;
            PersistentEntities = persistentEntities;
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

        public async UniTask DisposeAsync()
        {
            Query finalizeSDKComponentsQuery = EcsWorld.Query(new QueryDescription().WithAll<CRDTEntity>());

            Profiler.BeginSample("FinalizeSDKComponents");

            for (var i = 0; i < finalizeWorldSystems.Count; i++)
            {
                if (performanceBudget.TrySpendBudget() == false)
                    await UniTask.NextFrame();

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
