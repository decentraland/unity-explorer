using Arch.Core;
using Arch.SystemGroups;
using CRDT;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using DCL.PluginSystem.World;
using DCL.Profiling;
using ECS.LifeCycle;
using System;
using System.Collections.Generic;
using System.Text;
using SystemGroups.Visualiser;
using Profiler = UnityEngine.Profiling.Profiler;

namespace SceneRunner.ECSWorld
{
    public readonly struct ECSWorldFacade : IBudgetedDisposable
    {
        private static readonly Dictionary<Type, string> LABELS = new ();

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

            for (var i = 0; i < finalizeWorldSystems.Count; i++)
            {
                var system = finalizeWorldSystems[i]!;
                string label = LabelOfType(system.GetType());

                if (system.IsBudgetedFinalizeSupported)
                {
                    var enumerator = DisposeWithBudget(finalizeSDKComponentsQuery, system, label, budget, cleanUpMarker);
                    while (enumerator.MoveNext()) yield return enumerator.Current;
                }
                else
                {
                    DisposeImmediately(finalizeSDKComponentsQuery, system, label);
                    yield return new Unit();
                }
            }

            SystemGroupSnapshot.Instance.Unregister(systemGroupWorld);

            systemGroupWorld.Dispose();
            EcsWorld.Dispose();
            yield return new Unit();
        }

        private static IEnumerator<Unit> DisposeWithBudget(Query query, IFinalizeWorldSystem finalizeWorldSystem, string label, IPerformanceBudget budget, CleanUpMarker cleanUpMarker)
        {
            do
            {
                cleanUpMarker.Purify();

                using (ProfilerSampleScope.New("FinalizeSDKComponents/ByBudget"))
                using (ProfilerSampleScope.New(label))
                {
                    // We must be able to finalize world no matter what
                    // Marker being mutated inside
                    try { finalizeWorldSystem.BudgetedFinalizeComponents(in query, budget, cleanUpMarker); }
                    catch (Exception e)
                    {
                        ReportHub.LogException(e, ReportCategory.ECS);
                        yield break;
                    }
                }

                yield return new Unit();
            }

            // Clean until it's fully cleaned
            while (cleanUpMarker.IsFullyCleaned == false);
        }

        private static void DisposeImmediately(Query query, IFinalizeWorldSystem system, string label)
        {
            Profiler.BeginSample("FinalizeSDKComponents/Immediately");
            Profiler.BeginSample(label);

            // We must be able to finalize world no matter what
            try { system.FinalizeComponents(query); }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.ECS); }

            Profiler.EndSample();
            Profiler.EndSample();
        }

        private static string LabelOfType(Type type)
        {
            if (LABELS.TryGetValue(type, out string? value) == false)
            {
                var sb = new StringBuilder();
                sb.Append("FinalizeSDKComponents/").Append(type.Name);

                var generic = type.IsGenericType ? type.GetGenericArguments() : Array.Empty<Type>();

                if (generic.Length > 0)
                {
                    sb.Append("<");

                    for (int i = 0; i < generic.Length; i++)
                    {
                        var genericType = generic[i];
                        sb.Append(genericType.Name);
                        bool isLast = i == generic.Length - 1;

                        if (isLast == false)
                            sb.Append(", ");
                    }

                    sb.Append(">");
                }

                LABELS[type] = value = sb.ToString();
            }

            return value!;
        }
    }
}
