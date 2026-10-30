// TRUST_WEBGL_THREAD_SAFETY_FLAG

using DCL.Diagnostics;
using DCL.Utility.Types;
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

        private Option<AcquisitionInfo> currentAcquisitionInfo;
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

            // Enqueue under the monitor; it can't be held during the wait below or Release could never signal
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
            var timedOut = false;

            if (shouldWait)
                timedOut = !owner.Wait(TIMEOUT, cts.Token, out bool wasCancelled) && !wasCancelled;

            // Re-taken after the wait to resolve the timeout and claim ownership as one step
            lock (monitor)
            {
                // Release may have signaled this owner after the wait timed out but before the monitor was taken
                if (timedOut && !(queue.TryPeek(out Owner? head) && head == owner))
                {
                    // A waiter left in the queue would never be released and block every owner behind it
                    RemoveFromQueue(owner);

                    int requestingThreadId = NativeThread.CurrentId;
                    int owningThreadId = SYNC_OWNERSHIP.TryGetValue(syncId, out int ownerThread) ? ownerThread : -1;

                    string currentOwner = currentAcquisitionInfo.Has
                        ? $"\"{currentAcquisitionInfo.Value.Owner.Name}\" takes too long: {(DateTime.Now - currentAcquisitionInfo.Value.AcquiredAt).TotalSeconds}"
                        : $"none, next in queue: \"{head?.Name}\"";

                    syncLogsBuffer.Print();
                    throw new TimeoutException($"{nameof(MultiThreadSync)} timeout, cannot acquire for: {owner.Name}, current owner: {currentOwner}. Owning thread: {owningThreadId}, requesting thread: {requestingThreadId}");
                }

                currentAcquisitionInfo = Option<AcquisitionInfo>.Some(new AcquisitionInfo(owner, DateTime.Now));
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
                if (queue.TryDequeue(out Owner? finishedWaiter))
                {
                    // The one releasing should be the one at the top of the queue
                    if (owner != finishedWaiter)
                    {
                        syncLogsBuffer.Print();
                        throw new OwnerMismatchException(owner, finishedWaiter);
                    }

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

                currentAcquisitionInfo = Option<AcquisitionInfo>.None;
                SYNC_OWNERSHIP.TryRemove(syncId, out _);
            }
        }

        private void RemoveFromQueue(Owner owner)
        {
            int count = queue.Count;

            for (var i = 0; i < count; i++)
            {
                Owner queued = queue.Dequeue();

                if (queued != owner)
                    queue.Enqueue(queued);
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

                if (DateTime.Now - start > TIMEOUT)
                    throw new TimeoutException($"{nameof(MultiThreadSync)} source {source.Name} took too much time! cannot release for: {source.Name}. Releasing thread: {NativeThread.CurrentId}");
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
                    // Cleared first: Dispose throws when the scope was held too long, and the scope must not be released twice
                    isScoped = false;
                    scope.Dispose();
                }
            }
        }
    }
#endif
}
