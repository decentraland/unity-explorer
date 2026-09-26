using Arch.Core;
using Arch.SystemGroups;
using ECS.LifeCycle;
using ECS.Prioritization.Components;
using System;
using System.Collections.Generic;
using System.Threading;
using SystemGroups.Visualiser;

namespace Global.Dynamic
{
    /// <summary>
    ///     Represents a global world that must exist in a single instance and be cleared when the realm changes
    /// </summary>
    public class GlobalWorld : IDisposable
    {
        public static readonly string WORLD_NAME = "GLOBAL";
        public readonly World EcsWorld;
        public readonly IReadOnlyList<IFinalizeWorldSystem> FinalizeWorldSystems;

#if UNITY_EDITOR
        public static World ECSWorldInstance
        {
            get;
            private set;
        }
#endif

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

#if UNITY_EDITOR
            ECSWorldInstance = EcsWorld;
#endif
        }

        public void Dispose()
        {
            destroyCancellationSource.Cancel();
            worldSystems.Dispose();

            SystemGroupSnapshot.Instance.Unregister(worldSystems);

            EcsWorld.Dispose();
        }

        public void Clear()
        {
            cameraSamplingData.Clear();
            realmSamplingData.Clear();
        }
    }
}
