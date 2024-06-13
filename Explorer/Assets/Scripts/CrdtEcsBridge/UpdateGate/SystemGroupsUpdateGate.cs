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

        private HashSet<Type> openGroups;

        // Ensure that the gate will be opened from the beginning of the next frame,
        // If we open it in the middle of the current frame the update order will be broken
        private long keepOpenFrame;

        internal IReadOnlyCollection<Type> OpenGroups => openGroups;

        public SystemGroupsUpdateGate()
        {
            openGroups = POOL.Get();
        }

        public void Dispose()
        {
            if (openGroups == null) return;

            POOL.Release(openGroups);
            openGroups = null;
        }

        public bool ShouldThrottle(Type systemGroupType, in TimeProvider.Info timeInfo)
        {
            // Close the group so it won't be updated unless the gate is opened again
            // Sync is required as ShouldThrottle is called from the main thread
            lock (openGroups)
            {
                // Let systems run in the remaining of the current frame
                if (Time.frameCount < keepOpenFrame)
                    return false;

                // Otherwise, just let them run once
                return !openGroups.Remove(systemGroupType);
            }
        }

        public void OnSystemGroupUpdateFinished(Type systemGroupType, bool wasThrottled) { }

        public void Open()
        {
            // Already disposed
            if (openGroups == null)
                return;

            // Synchronization is required as Open is called from the scene thread
            lock (openGroups)
            {
                // Open all system groups to execution
                openGroups.Add(typeof(InitializationSystemGroup));
                openGroups.Add(typeof(SimulationSystemGroup));
                openGroups.Add(typeof(PresentationSystemGroup));
                openGroups.Add(typeof(PhysicsSystemGroup));
                openGroups.Add(typeof(PostPhysicsSystemGroup));
                openGroups.Add(typeof(PreRenderingSystemGroup));

                keepOpenFrame = MultithreadingUtility.FrameCount + 1;
            }
        }
    }
}
