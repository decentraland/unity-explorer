using Arch.Core;
using Arch.SystemGroups;
using CRDT;
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
    public readonly struct ECSWorldFacade : IBudgetedDisposable
    {
        public readonly World EcsWorld;
        public readonly PersistentEntities PersistentEntities;

        private readonly IReadOnlyList<IFinalizeWorldSystem> finalizeWorldSystems;
        private readonly IReadOnlyList<ISceneIsCurrentListener> sceneIsCurrentListeners;
        private readonly IPerformanceBudget budget;
        private readonly CleanUpMarker cleanUpMarker;

        private readonly SystemGroupWorld systemGroupWorld;

        public ECSWorldFacade(
            SystemGroupWorld systemGroupWorld,
            World ecsWorld,
            PersistentEntities persistentEntities,
            IReadOnlyList<IFinalizeWorldSystem> finalizeWorldSystems,
            IReadOnlyList<ISceneIsCurrentListener> sceneIsCurrentListeners,
            IPerformanceBudget budget)
        {
            cleanUpMarker = new CleanUpMarker();
            this.systemGroupWorld = systemGroupWorld;
            EcsWorld = ecsWorld;
            this.finalizeWorldSystems = finalizeWorldSystems;
            this.sceneIsCurrentListeners = sceneIsCurrentListeners;
            this.budget = budget;
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

        public IEnumerator<Unit> BudgetedDispose()
        {
            Query finalizeSDKComponentsQuery = EcsWorld.Query(new QueryDescription().WithAll<CRDTEntity>());

            Profiler.BeginSample("FinalizeSDKComponents");

            for (var i = 0; i < finalizeWorldSystems.Count; i++)
            {
                var enumerator = FinalizeSystem(finalizeSDKComponentsQuery, finalizeWorldSystems[i]!, budget, cleanUpMarker);
                while (enumerator.MoveNext()) yield return enumerator.Current;
            }

            Profiler.EndSample();

            SystemGroupSnapshot.Instance.Unregister(systemGroupWorld);

            systemGroupWorld.Dispose();
            EcsWorld.Dispose();
            yield return new Unit();
        }

        private static IEnumerator<Unit> FinalizeSystem(Query query, IFinalizeWorldSystem finalizeWorldSystem, IPerformanceBudget budget, CleanUpMarker cleanUpMarker)
        {
            do
            {
                cleanUpMarker.Purify();

                // We must be able to finalize world no matter what
                // Marker being mutated inside
                try { finalizeWorldSystem.FinalizeComponents(in query, budget, cleanUpMarker); }
                catch (Exception e)
                {
                    ReportHub.LogException(e, ReportCategory.ECS);
                    yield break;
                }

                yield return new Unit();
            }

            // Clean until it's fully cleaned
            while (cleanUpMarker.IsFullyCleaned == false);
        }
    }
}
