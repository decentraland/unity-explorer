using Arch.SystemGroups.DefaultSystemGroups;
using Arch.SystemGroups.UnityBridge;
using DCL.Optimization.Pools;
using DCL.Optimization.ThreadSafePool;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utility.Multithreading;

namespace CrdtEcsBridge.UpdateGate
{
    /// <summary>
    ///     <inheritdoc cref="ISystemGroupsUpdateGate" />
    /// </summary>
    public class SystemGroupsUpdateGate : ISystemGroupsUpdateGate
    {
        private static readonly ThreadSafeHashSetPool<Type> POOL = new (SystemGroupsUtils.Count, PoolConstants.SCENES_COUNT);
        private static readonly Type[] ALL_THROTTLED_GROUPS =
        {
            typeof(InitializationSystemGroup),
            typeof(SimulationSystemGroup),
            typeof(PresentationSystemGroup),
            typeof(PhysicsSystemGroup),
            typeof(PostPhysicsSystemGroup),
            typeof(PreRenderingSystemGroup),
        };

        private HashSet<Type> openGroups = POOL.Get();

        // Ensure that the gate will be opened from the beginning of the next frame,
        // If we open it in the middle of the current frame the update order will be broken
        private long keepOpenFrame;

        internal IReadOnlyCollection<Type> OpenGroups => openGroups;

        public void Dispose()
        {
            if (openGroups == null) return;

            POOL.Release(openGroups);

            openGroups = null;
        }

        // Close the group so it won't be updated unless the gate is opened again
        public bool ShouldThrottle(Type systemGroupType, in TimeProvider.Info _)
        {
            // Let systems run in the remaining of the current frame
            if (Time.frameCount < keepOpenFrame)
                return false;

            return !openGroups.Contains(systemGroupType);
        }

        public void OnSystemGroupUpdateFinished(Type systemGroupType, bool wasThrottled)
        {
            if (wasThrottled)
                return;

            // Let systems run in the remaining of the current frame
            if (Time.frameCount < keepOpenFrame)
                return;

            // Close only at the end of the full frame pass
            lock (openGroups) { openGroups.Remove(systemGroupType); }
        }

        public void Open()
        {
            // Already disposed
            if (openGroups == null)
                return;

            // Synchronization is required as Open is called from the scene thread
            lock (openGroups)
            {
                // Open all system groups to execution
                openGroups.UnionWith(ALL_THROTTLED_GROUPS);
                keepOpenFrame = MultithreadingUtility.FrameCount + 1;
            }
        }
    }
}
