using Arch.Core;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.Components.Special;
using ECS.LifeCycle;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;

namespace SceneRunner.ECSWorld
{
    public readonly struct ECSWorldFacade : IDisposable
    {
        public readonly World EcsWorld;
        private readonly IReadOnlyList<IFinalizeWorldSystem> finalizeWorldSystems;

        private readonly SystemGroupWorld systemGroupWorld;

        public ECSWorldFacade(SystemGroupWorld systemGroupWorld, World ecsWorld, params IFinalizeWorldSystem[] finalizeWorldSystems)
        {
            this.systemGroupWorld = systemGroupWorld;
            this.EcsWorld = ecsWorld;
            this.finalizeWorldSystems = finalizeWorldSystems;
        }

        public void Initialize()
        {
            systemGroupWorld.Initialize();
        }

        public void Dispose()
        {
            var finalizeSDKComponentsQuery = EcsWorld.Query(new QueryDescription().WithAll<CRDTEntity>());

            Profiler.BeginSample("FinalizeSDKComponents");

            for (var i = 0; i < finalizeWorldSystems.Count; i++)
                finalizeWorldSystems[i].FinalizeSDKComponents(in finalizeSDKComponentsQuery);

            Profiler.EndSample();

            systemGroupWorld.Dispose();
            EcsWorld.Dispose();
        }
    }
}
