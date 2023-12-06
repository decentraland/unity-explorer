using Arch.Core;
using Arch.SystemGroups;
using CRDT;
using ECS.LifeCycle;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using SystemGroups.Visualiser;


namespace SceneRunner.ECSWorld
{
    public readonly struct ECSWorldFacade : IDisposable
    {
        public readonly World EcsWorld;
        private readonly IReadOnlyList<IFinalizeWorldSystem> finalizeWorldSystems;

        private readonly SystemGroupWorld systemGroupWorld;

        public ECSWorldFacade(
            SystemGroupWorld systemGroupWorld,
            World ecsWorld,
            IReadOnlyList<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            this.systemGroupWorld = systemGroupWorld;
            EcsWorld = ecsWorld;
            this.finalizeWorldSystems = finalizeWorldSystems;
        }

        public void Initialize()
        {
            systemGroupWorld.Initialize();
        }

        public void Dispose()
        {
            Query finalizeSDKComponentsQuery = EcsWorld.Query(new QueryDescription().WithAll<CRDTEntity>());

            Profiler.BeginSample("FinalizeSDKComponents");

            for (var i = 0; i < finalizeWorldSystems.Count; i++)
            {
                // We must be able to finalize world no matter what
                try { finalizeWorldSystems[i].FinalizeComponents(in finalizeSDKComponentsQuery); }
                catch (Exception e) { Debug.LogException(e); }
            }

            Profiler.EndSample();

            SystemGroupSnapshot.Instance.Unregister(systemGroupWorld);

            systemGroupWorld.Dispose();
            EcsWorld.Dispose();
        }
    }
}
