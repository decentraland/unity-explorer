using Arch.Core;
using Arch.SystemGroups;
using ECS.LifeCycle;
using ECS.Prioritization.Components;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Global.Dynamic
{
    /// <summary>
    ///     Represents a global world that must exist in a single instance and be cleared when the realm changes
    /// </summary>
    public class GlobalWorld : IDisposable
    {
        public readonly World EcsWorld;
        public readonly IReadOnlyList<IFinalizeWorldSystem> FinalizeWorldSystems;

        private readonly CancellationTokenSource destroyCancellationSource;

        private readonly CameraSamplingData cameraSamplingData;
        private readonly RealmSamplingData realmSamplingData;
        private readonly SystemGroupWorld worldSystems;

        public GlobalWorld(World ecsWorld, SystemGroupWorld systemGroupWorld,
            IReadOnlyList<IFinalizeWorldSystem> finalizeWorldSystems,
            CameraSamplingData cameraSamplingData, RealmSamplingData realmSamplingData,
            CancellationTokenSource destroyCancellationSource)
        {
            EcsWorld = ecsWorld;
            this.cameraSamplingData = cameraSamplingData;
            this.realmSamplingData = realmSamplingData;
            this.destroyCancellationSource = destroyCancellationSource;
            FinalizeWorldSystems = finalizeWorldSystems;
            worldSystems = systemGroupWorld;
        }

        public void Clear()
        {
            cameraSamplingData.Clear();
            realmSamplingData.Clear();
        }

        public void Dispose()
        {
            destroyCancellationSource.Cancel();
            worldSystems.Dispose();
            EcsWorld.Dispose();
        }
    }
}
