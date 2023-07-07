using Arch.SystemGroups.DefaultSystemGroups;
using Arch.SystemGroups.UnityBridge;
using System;
using System.Collections.Generic;
using Utility.Pool;
using Utility.ThreadSafePool;

namespace CrdtEcsBridge.UpdateGate
{
    /// <summary>
    ///     <inheritdoc cref="ISystemGroupsUpdateGate" />
    /// </summary>
    public class SystemGroupsUpdateGate : ISystemGroupsUpdateGate
    {
        private static readonly ThreadSafeHashSetPool<Type> POOL = new (SystemGroupsUtils.Count, PoolConstants.SCENES_COUNT);

        private HashSet<Type> openGroups;

        public SystemGroupsUpdateGate()
        {
            openGroups = POOL.Get();
        }

        internal IReadOnlyCollection<Type> OpenGroups => openGroups;

        public bool ShouldThrottle(Type systemGroupType, in TimeProvider.Info timeInfo)
        {
            // Close the group so it won't be updated unless the gate is opened again
            // Sync is required as ShouldThrottle is called from the main thread
            lock (openGroups) { return openGroups.Remove(systemGroupType); }
        }

        public void OnSystemGroupUpdateFinished(Type systemGroupType, bool wasThrottled) { }

        public void Open()
        {
            // Synchronization is required as Open is called from the scene thread
            lock (openGroups)
            {
                // Open all system groups to execution
                openGroups.Add(typeof(InitializationSystemGroup));
                openGroups.Add(typeof(SimulationSystemGroup));
                openGroups.Add(typeof(PresentationSystemGroup));
                openGroups.Add(typeof(PhysicsSystemGroup));
                openGroups.Add(typeof(PostPhysicsSystemGroup));
                openGroups.Add(typeof(PostRenderingSystemGroup));
            }
        }

        public void Dispose()
        {
            POOL.Release(openGroups);
            openGroups = null;
        }
    }
}
