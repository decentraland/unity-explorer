using DCL.Diagnostics;
using System;
using System.Threading;
using DCL.Optimization.ThreadSafePool;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine.Profiling;

namespace Utility.Multithreading
{
    public class SyncLogsBuffer
    {
        public readonly SceneShortInfo sceneShortInfo;
        private readonly int logsToKeep;
        private readonly DateTime creationTime;

        private readonly LinkedList<Entry> circularBuffer;

        public SyncLogsBuffer(SceneShortInfo sceneShortInfo, int logsToKeep)
        {
            this.sceneShortInfo = sceneShortInfo;
            this.logsToKeep = logsToKeep;
            creationTime = DateTime.Now;

            circularBuffer = new LinkedList<Entry>();
        }

        public void Report(string eventLog, string source)
        {
            if (circularBuffer.Count >= logsToKeep)
                circularBuffer.RemoveFirst();

            circularBuffer.AddLast(new Entry(eventLog, DateTime.Now - creationTime, source, Thread.CurrentThread.ManagedThreadId));
        }

        public void Print()
        {
            var reportData = new ReportData(ReportCategory.SYNC, sceneShortInfo: sceneShortInfo);

            foreach (Entry entry in circularBuffer)
                ReportHub.Log(reportData, $"T: {entry.TimeSinceCreation.TotalSeconds}, {entry.EventLog} {entry.Source}");
        }

        private struct Entry
        {
            public readonly string EventLog;
            public readonly TimeSpan TimeSinceCreation;
            public readonly string Source;
            public readonly int ThreadId;

            public Entry(string eventLog, TimeSpan timeSinceCreation, string source, int threadId)
            {
                EventLog = eventLog;
                TimeSinceCreation = timeSinceCreation;
                Source = source;
                ThreadId = threadId;
            }
        }
    }

    public class MultithreadSync : IDisposable
    {
        private static readonly ProfilerMarker COMMON_SAMPLER;

        private static readonly TimeSpan MAX_LIMIT = TimeSpan.FromSeconds(10);

        private readonly object monitor = new ();

        private readonly Queue<Owner> queue = new ();

        private readonly SyncLogsBuffer syncLogsBuffer;

        private readonly CustomSampler perSceneSampler;

        private readonly CancellationTokenSource cts = new ();

        private bool acquired;
        private bool isDisposing;
        private (string? name, DateTime startedAt, long accuiredIndex) currentScope = new (null, DateTime.MinValue, 0);
        private long acquireCount;

        public bool Acquired
        {
            get
            {
                lock (monitor) { return acquired; }
            }
        }

        static MultithreadSync()
        {
            COMMON_SAMPLER = new ProfilerMarker("MultithreadSync.Wait");
        }

        public MultithreadSync(SceneShortInfo sceneInfo)
        {
            syncLogsBuffer = new SyncLogsBuffer(sceneInfo, 20);
            perSceneSampler = CustomSampler.Create("MultithreadSync.Wait " + sceneInfo.BaseParcel);
        }

        public void Dispose()
        {
            lock (monitor)
            {
                isDisposing = true;
                acquired = false;

                cts.SafeCancelAndDispose();

                // Dispose owners currently in use
                // Don't accept any new ones

                foreach (Owner owner in queue)
                    owner.Dispose();

                queue.Clear();
            }
        }

        private void Acquire(Owner owner)
        {
            long currentId;
            bool shouldWait;

            lock (monitor)
            {
#if SYNC_DEBUG
                syncLogsBuffer.Report("MultithreadSync Acquire start for:", owner.Name);
#endif
                acquireCount++;
                currentId = acquireCount;

                if (isDisposing)
                    throw new ObjectDisposedException(nameof(MultithreadSync));

                shouldWait = queue.Count > 0;
                queue.Enqueue(owner);
            }

            // There is already one thread doing work. Wait for the signal
            if (shouldWait)
            {
                if (!owner.Wait(MAX_LIMIT, cts.Token, out bool wasCancelled) && !wasCancelled)
                {
                    DateTime time = DateTime.Now;
                    (string? name, DateTime startedAt, long accuiredIndex) current = currentScope;
                    TimeSpan difference = time - current.startedAt;

                    lock (monitor) { syncLogsBuffer.Print(); }

                    throw new TimeoutException($"{nameof(MultithreadSync)} timeout, cannot acquire for: {owner.Name}, main context \"{current.name}\" id \"{current.accuiredIndex}\" takes too long: {difference.TotalSeconds}");
                }
            }

            lock (monitor)
            {
                acquired = true;
#if SYNC_DEBUG
                syncLogsBuffer.Report("MultithreadSync Acquire finished for:", owner.Name);
#endif
                currentScope = (owner.Name, DateTime.Now, currentId);
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

                    finishedWaiter.EventSlim.Reset();
                    acquired = false;

                    if (queue.TryPeek(out Owner? next))
                        next.EventSlim.Set(); // Signal the next waiter in line

#if SYNC_DEBUG
                    syncLogsBuffer.Report("MultithreadSync Release finished for:", source);
#endif
                }
#if SYNC_DEBUG
                else
                    syncLogsBuffer.Report("MultithreadSync Release finished CANNOT", source);
#endif
                currentScope = (null, DateTime.MinValue, 0);
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
            public readonly ManualResetEventSlim EventSlim = new (false);
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
                    return EventSlim.Wait(timeout, ct);
                }
                catch (OperationCanceledException)
                {
                    wasCancelled = true;
                    return false;
                }
            }

            public void Dispose()
            {
                EventSlim.Dispose();
            }
        }

        public readonly struct Scope : IDisposable
        {
            private readonly MultithreadSync multithreadSync;
            private readonly Owner source;
            private readonly DateTime start;

            public Scope(MultithreadSync multithreadSync, Owner source)
            {
                this.multithreadSync = multithreadSync;
                this.source = source;
                multithreadSync.Acquire(source);
                start = DateTime.Now;
            }

            public void Dispose()
            {
                multithreadSync.Release(source);

                if (DateTime.Now - start > MAX_LIMIT)
                    throw new TimeoutException($"{nameof(MultithreadSync)} source {source.Name} took too much time! cannot release for: {source.Name}");
            }
        }

        public class BoxedScope
        {
            private readonly MultithreadSync multithreadSync;
            private Scope scope;
            private bool isScoped;

            public BoxedScope(MultithreadSync multithreadSync)
            {
                this.multithreadSync = multithreadSync;
                scope = default(Scope);
                isScoped = false;
            }

            public void Acquire(Owner source)
            {
                scope = multithreadSync.GetScope(source);
                isScoped = true;
            }

            public void ReleaseIfAcquired()
            {
                if (isScoped)
                {
                    scope.Dispose();
                    isScoped = false;
                }
            }
        }
    }
}
