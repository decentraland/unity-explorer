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

        private HashSet<Type> openGroups = POOL.Get();
        private HashSet<Type> throttledThisFrameGroups = POOL.Get();

        // Ensure that the gate will be opened from the beginning of the next frame,
        // If we open it in the middle of the current frame the update order will be broken
        private long keepOpenFrame;

        internal IReadOnlyCollection<Type> OpenGroups => openGroups;

        public void Dispose()
        {
            if (openGroups == null) return;

            POOL.Release(openGroups);
            POOL.Release(throttledThisFrameGroups);

            openGroups = null;
            throttledThisFrameGroups = null;
        }

        // Close the group so it won't be updated unless the gate is opened again
        public bool ShouldThrottle(Type systemGroupType, in TimeProvider.Info _ = default)
        {
            // Let systems run in the remaining of the current frame
            if (Time.frameCount < keepOpenFrame)
                return false;

            // Otherwise, close group but let it run one frame (current)
            return !TryCloseThisFrame(systemGroupType);
        }

        private bool TryCloseThisFrame(Type systemGroupType)
        {
            if (throttledThisFrameGroups.Contains(systemGroupType)) return true;
            if (!openGroups.Contains(systemGroupType)) return false;

            // Sync is required as TryCloseThisFrame is called from the main thread
            lock (openGroups)
            {
                if (openGroups.Remove(systemGroupType))
                {
                    throttledThisFrameGroups.Add(systemGroupType);
                    return true;
                }
            }

            return false;
        }

        public void OnSystemGroupUpdateFinished(Type systemGroupType, bool wasThrottled)
        {
            throttledThisFrameGroups.Clear();
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
