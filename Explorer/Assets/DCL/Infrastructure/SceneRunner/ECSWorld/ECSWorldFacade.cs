using Arch.Core;
using Arch.SystemGroups;
using CRDT;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using DCL.PluginSystem.World;
using ECS.LifeCycle;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Profiling;
using SystemGroups.Visualiser;

namespace SceneRunner.ECSWorld
{
    public readonly struct ECSWorldFacade : IBudgetedDisposable
    {
        private static readonly Dictionary<Type, string> LABELS = new ();

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
            this.finalizeWorldSystems = finalizeWorldSystems;
            this.sceneIsCurrentListeners = sceneIsCurrentListeners;
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

                Profiler.BeginSample("FinalizeSDKComponents");
                Profiler.BeginSample(label);

                // We must be able to finalize world no matter what
                try { system.FinalizeComponents(in finalizeSDKComponentsQuery); }
                catch (Exception e) { ReportHub.LogException(e, ReportCategory.ECS); }

                Profiler.EndSample();
                Profiler.EndSample();

                yield return new Unit();
            }

            SystemGroupSnapshot.Instance.Unregister(systemGroupWorld);

            systemGroupWorld.Dispose();
            EcsWorld.Dispose();
            yield return new Unit();
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
