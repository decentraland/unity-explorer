// TRUST_WEBGL_THREAD_SAFETY_FLAG

using DCL.Diagnostics;
using System;
using System.Threading;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Unity.Profiling;
using Utility.Multithreading;
using UnityEngine.Profiling;

namespace Utility.Multithreading
{
#if !UNITY_WEBGL
    public class MultiThreadSync : IDisposable
    {
        internal readonly struct AcquisitionInfo
        {
            public readonly Owner Owner;
            public readonly DateTime AcquiredAt;

            public AcquisitionInfo(Owner owner, DateTime acquiredAt)
            {
                Owner = owner;
                AcquiredAt = acquiredAt;
            }
        }

        private static readonly ProfilerMarker COMMON_SAMPLER;

        private static readonly TimeSpan TIMEOUT = TimeSpan.FromSeconds(10);

        /// <summary>
        ///     Maps each <see cref="MultiThreadSync" /> instance (by its <see cref="syncId" />) to the managed thread id
        ///     that currently owns it. Used to surface ownership in timeout diagnostics. A missing key means unowned.
        /// </summary>
        private static readonly DCLConcurrentDictionary<int, int> SYNC_OWNERSHIP = new ();

        private static int nextSyncId;

        private readonly int syncId;

        private readonly object monitor = new ();

        private readonly Queue<Owner> queue = new ();

        private readonly SyncLogsBuffer syncLogsBuffer;

        private readonly CustomSampler perSceneSampler;

        private readonly CancellationTokenSource cts = new ();

        private AcquisitionInfo? currentAcquisitionInfo;
        private bool isDisposing;

        static MultiThreadSync()
        {
            COMMON_SAMPLER = new ProfilerMarker("MultithreadSync.Wait");
        }

        public MultiThreadSync(SceneShortInfo sceneInfo)
        {
            syncId = Interlocked.Increment(ref nextSyncId);
            syncLogsBuffer = new SyncLogsBuffer(sceneInfo, 20);
            perSceneSampler = CustomSampler.Create("MultithreadSync.Wait " + sceneInfo.BaseParcel);
        }

        public void Dispose()
        {
            lock (monitor)
            {
                isDisposing = true;

                cts.SafeCancelAndDispose();

                // Dispose owners currently in use
                // Don't accept any new ones

                foreach (Owner owner in queue)
                    owner.Dispose();

                queue.Clear();

                SYNC_OWNERSHIP.TryRemove(syncId, out _);
            }
        }

        private void Acquire(Owner owner)
        {
            bool shouldWait;

            lock (monitor)
            {
#if SYNC_DEBUG
                syncLogsBuffer.Report("MultithreadSync Acquire start for:", owner.Name);
#endif

                if (isDisposing)
                    throw new ObjectDisposedException(nameof(MultiThreadSync));

                shouldWait = queue.Count > 0;
                queue.Enqueue(owner);
            }

            // There is already one thread doing work. Wait for the signal
            if (shouldWait)
            {
                if (!owner.Wait(TIMEOUT, cts.Token, out bool wasCancelled))
                {
                    lock (monitor)
                    {
                        // On disposal the wait is cancelled: withdraw the entry and leave without taking
                        // ownership so the static ownership table is not repopulated after Dispose removed the syncId
                        if (wasCancelled)
                        {
                            RemoveFromQueue(owner);
                            return;
                        }

                        // Release may have signalled this waiter between the wait expiring and re-entering
                        // the monitor: the acquisition succeeded, just late
                        bool ownershipHandedOver = queue.TryPeek(out Owner? head) && ReferenceEquals(head, owner) && owner.IsSignalled;

                        if (!ownershipHandedOver)
                        {
                            // The waiter never acquired the sync: withdraw its entry so an abandoned
                            // owner can never stall or desynchronize subsequent acquisitions
                            RemoveFromQueue(owner);

                            DateTime time = DateTime.Now;
                            int requestingThreadId = NativeThread.CurrentId;
                            int owningThreadId = SYNC_OWNERSHIP.TryGetValue(syncId, out int ownerThread) ? ownerThread : -1;

                            string currentOwnerDescription = currentAcquisitionInfo.HasValue
                                ? $"\"{currentAcquisitionInfo.Value.Owner.Name}\" takes too long: {(time - currentAcquisitionInfo.Value.AcquiredAt).TotalSeconds}"
                                : "unowned";

                            syncLogsBuffer.Print();
                            throw new TimeoutException($"{nameof(MultiThreadSync)} timeout, cannot acquire for: {owner.Name}, current owner: {currentOwnerDescription}. Owning thread: {owningThreadId}, requesting thread: {requestingThreadId}");
                        }
                    }
                }
            }

            lock (monitor)
            {
                // Disposal may have raced the successful wait: ownership must not be recorded
                // for a disposed sync (Release no-ops on isDisposing, so it would never be cleared)
                if (isDisposing)
                    return;

                currentAcquisitionInfo = new AcquisitionInfo(owner, DateTime.Now);
                SYNC_OWNERSHIP[syncId] = NativeThread.CurrentId;

#if SYNC_DEBUG
                syncLogsBuffer.Report("MultithreadSync Acquire finished for:", owner.Name);
#endif
            }
        }

        /// <summary>
        ///     Removes the first queued occurrence of <paramref name="owner" /> without disturbing the order of the
        ///     other waiters, and resets the owner's event so a pending signal cannot leak into its next acquisition.
        ///     If the removed entry had already been handed the baton (it was the signalled head), the baton is
        ///     passed on to the next waiter so the chain never stalls. Must be called under <see cref="monitor" />.
        /// </summary>
        private void RemoveFromQueue(Owner owner)
        {
            int count = queue.Count;
            var removed = false;
            var removedSignalledHead = false;

            for (var i = 0; i < count; i++)
            {
                Owner candidate = queue.Dequeue();

                if (!removed && ReferenceEquals(candidate, owner))
                {
                    removed = true;
                    removedSignalledHead = i == 0 && owner.IsSignalled;
                    continue;
                }

                queue.Enqueue(candidate);
            }

            // A disposed sync has already cleared the queue and disposed the owners' events:
            // when nothing was removed the owner's event must not be touched
            if (!removed)
                return;

            owner.Reset();

            if (removedSignalledHead && queue.TryPeek(out Owner? next))
                next.Set();
        }

        private void Release(Owner owner)
        {
            lock (monitor)
            {
                string source = owner.Name;

#if SYNC_DEBUG
                syncLogsBuffer.Report("MultithreadSync Release start for:", source);
#endif

                if (isDisposing)
                    return;

                try
                {
                    // If the queue is empty, then our logic is wrong
                    if (queue.TryPeek(out Owner? finishedWaiter))
                    {
                        // The one releasing should be the one at the top of the queue.
                        // Validate before dequeuing so a mismatch doesn't consume another owner's entry
                        if (owner != finishedWaiter)
                        {
                            syncLogsBuffer.Print();
                            throw new OwnerMismatchException(owner, finishedWaiter);
                        }

                        queue.Dequeue();
                        finishedWaiter.Reset();

                        if (queue.TryPeek(out Owner? next))
                            next.Set(); // Signal the next waiter in line

#if SYNC_DEBUG
                        syncLogsBuffer.Report("MultithreadSync Release finished for:", source);
#endif
                    }
#if SYNC_DEBUG
                    else
                        syncLogsBuffer.Report("MultithreadSync Release finished CANNOT", source);
#endif
                }
                finally
                {
                    // Ownership must be cleared even on a mismatch so no stale entry survives in the table
                    currentAcquisitionInfo = null;
                    SYNC_OWNERSHIP.TryRemove(syncId, out _);
                }
            }
        }

        public Scope GetScope(Owner source)
        {
            COMMON_SAMPLER.Begin(source.Name);
            perSceneSampler.Begin();

            Scope scope;

            try { scope = new Scope(this, source); }
            finally
            {
                perSceneSampler.End();
                COMMON_SAMPLER.End();
            }

            return scope;
        }

        public static void AppendOwnershipTable(StringBuilder sb)
        {
            sb.Append("MultiThreadSync: ");

            var any = false;

            foreach (KeyValuePair<int, int> entry in SYNC_OWNERSHIP)
            {
                sb.Append(entry.Key).Append("=").Append(entry.Value).Append("|");
                any = true;
            }

            if (!any)
                sb.Append("(none owned)");
        }

        public class OwnerMismatchException : Exception
        {
            private readonly Owner releasingOwner;
            private readonly Owner firstInQueueOwner;

            public override string Message => $"Releasing owner {releasingOwner.Name} != Queue owner {firstInQueueOwner.Name}";

            public OwnerMismatchException(Owner releasingOwner, Owner firstInQueueOwner)
            {
                this.releasingOwner = releasingOwner;
                this.firstInQueueOwner = firstInQueueOwner;
            }
        }

        public class Owner
        {
            private readonly ManualResetEventSlim eventSlim = new (false);
            public readonly string Name;

            /// <summary>
            ///     Whether the owner has been signalled to proceed and has not consumed the signal yet.
            /// </summary>
            public bool IsSignalled => eventSlim.IsSet;

            public Owner(string name)
            {
                Name = name;
            }

            public bool Wait(TimeSpan timeout, CancellationToken ct, out bool wasCancelled)
            {
                try
                {
                    wasCancelled = false;

                    // Don't time-out if we are debugging (there is no better way to detect if we are actually in a breakpoint)
                    if (Debugger.IsAttached)
                    {
                        eventSlim.Wait(ct);
                        return true;
                    }

                    return eventSlim.Wait(timeout, ct);
                }
                catch (OperationCanceledException)
                {
                    wasCancelled = true;
                    return false;
                }
            }

            public void Set()
            {
                eventSlim.Set();
            }

            public void Reset()
            {
                eventSlim.Reset();
            }

            public void Dispose()
            {
                eventSlim.Dispose();
            }
        }

        public readonly struct Scope : IDisposable
        {
            private readonly MultiThreadSync multiThreadSync;
            private readonly Owner source;
            private readonly DateTime start;

            public Scope(MultiThreadSync multiThreadSync, Owner source)
            {
                this.multiThreadSync = multiThreadSync;
                this.source = source;
                multiThreadSync.Acquire(source);
                start = DateTime.Now;
            }

            public void Dispose()
            {
                multiThreadSync.Release(source);

                // The release itself succeeded: an over-long hold is diagnostic information,
                // throwing here would leave the caller's scope-tracking state inconsistent
                if (DateTime.Now - start > TIMEOUT)
                    ReportHub.LogWarning(
                        new ReportData(ReportCategory.SYNC, sceneShortInfo: multiThreadSync.syncLogsBuffer.sceneShortInfo),
                        $"{nameof(MultiThreadSync)} scope for {source.Name} was held for {(DateTime.Now - start).TotalSeconds}s, longer than {TIMEOUT.TotalSeconds}s. Releasing thread: {NativeThread.CurrentId}");
            }
        }

        public class BoxedScope
        {
            private readonly MultiThreadSync multiThreadSync;
            private Scope scope;
            private bool isScoped;

            public BoxedScope(MultiThreadSync multiThreadSync)
            {
                this.multiThreadSync = multiThreadSync;
                scope = default(Scope);
                isScoped = false;
            }

            public void Acquire(Owner source)
            {
                scope = multiThreadSync.GetScope(source);
                isScoped = true;
            }

            public void ReleaseIfAcquired()
            {
                if (!isScoped)
                    return;

                // Cleared before disposing so a throwing release can never leave a stale scope
                // that would be double-released on the next call
                isScoped = false;
                scope.Dispose();
            }
        }
    }
#endif
}
