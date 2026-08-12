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
                if (!owner.Wait(TIMEOUT, cts.Token, out bool wasCancelled) && !wasCancelled)
                {
                    lock (monitor)
                    {
                        // The previous owner may have signalled this waiter between the wait expiring
                        // and re-entering the monitor. In that case the acquisition succeeded, just late
                        bool ownershipHandedOver = queue.TryPeek(out Owner? head) && head == owner && owner.IsSet;

                        if (!ownershipHandedOver)
                        {
                            // Withdraw the entry enqueued by this acquisition and clear any pending signal,
                            // so the queue never holds entries nobody is waiting on and the reused owner
                            // starts its next acquisition from a clean state
                            RemoveTimedOutWaiter(owner);

                            DateTime time = DateTime.Now;
                            AcquisitionInfo? current = currentAcquisitionInfo;
                            string currentOwnerName = current?.Owner.Name ?? "<none>";
                            double heldSeconds = current.HasValue ? (time - current.Value.AcquiredAt).TotalSeconds : 0d;

                            int requestingThreadId = NativeThread.CurrentId;
                            int owningThreadId = SYNC_OWNERSHIP.TryGetValue(syncId, out int ownerThread) ? ownerThread : -1;

                            syncLogsBuffer.Print();
                            throw new TimeoutException($"{nameof(MultiThreadSync)} timeout, cannot acquire for: {owner.Name}, current owner: \"{currentOwnerName}\" takes too long: {heldSeconds}. Owning thread: {owningThreadId}, requesting thread: {requestingThreadId}");
                        }
                    }
                }
            }

            lock (monitor)
            {
                currentAcquisitionInfo = new AcquisitionInfo(owner, DateTime.Now);
                SYNC_OWNERSHIP[syncId] = NativeThread.CurrentId;

#if SYNC_DEBUG
                syncLogsBuffer.Report("MultithreadSync Acquire finished for:", owner.Name);
#endif
            }
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

                currentAcquisitionInfo = null;
                SYNC_OWNERSHIP.TryRemove(syncId, out _);
            }
        }

        /// <summary>
        ///     Removes a single entry of the given owner from the queue, preserving the order of the remaining
        ///     entries, and resets the owner's event so a pending signal cannot leak into its next acquisition.
        ///     Must be called under <see cref="monitor" />.
        /// </summary>
        private void RemoveTimedOutWaiter(Owner owner)
        {
            int count = queue.Count;
            var removed = false;
            var removedHead = false;

            for (var i = 0; i < count; i++)
            {
                Owner entry = queue.Dequeue();

                if (!removed && entry == owner)
                {
                    removed = true;
                    removedHead = i == 0;
                    continue;
                }

                queue.Enqueue(entry);
            }

            owner.Reset();

            // If the removed entry was at the head, ownership passes to the next waiter
            if (removedHead && queue.TryPeek(out Owner? next))
                next.Set();
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

            public bool IsSet => eventSlim.IsSet;

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

                // The scope is already released at this point: a long hold is a diagnostic,
                // not a failure of the release itself
                if (DateTime.Now - start > TIMEOUT)
                    ReportHub.LogError(ReportCategory.SYNC, $"{nameof(MultiThreadSync)} source {source.Name} took too much time! Held for: {(DateTime.Now - start).TotalSeconds}s. Releasing thread: {NativeThread.CurrentId}");
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
                if (isScoped)
                {
                    // Clear the flag before disposing so a throwing release cannot leave a stale
                    // scope behind that would be double-released on the next call
                    isScoped = false;
                    scope.Dispose();
                }
            }
        }
    }
#endif
}
