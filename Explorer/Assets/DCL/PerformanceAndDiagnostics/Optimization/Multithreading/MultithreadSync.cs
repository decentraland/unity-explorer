using DCL.Diagnostics;
using System;
using System.Threading;
using DCL.Optimization.ThreadSafePool;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine.Profiling;

namespace Utility.Multithreading
{
    public class SyncLogsBuffer
    {
        private readonly SceneShortInfo sceneShortInfo;
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
            lock (circularBuffer)
            {
                if (circularBuffer.Count >= logsToKeep)
                    circularBuffer.RemoveFirst();

                circularBuffer.AddLast(new Entry(eventLog, DateTime.Now - creationTime, source));
            }
        }

        public void Print()
        {
            lock (circularBuffer)
            {
                var reportData = new ReportData(ReportCategory.SYNC, sceneShortInfo: sceneShortInfo);

                foreach (Entry entry in circularBuffer)
                    ReportHub.Log(reportData, $"T: {entry.TimeSinceCreation.TotalSeconds}, {entry.EventLog} {entry.Source}");
            }
        }

        private struct Entry
        {
            public readonly string EventLog;
            public readonly TimeSpan TimeSinceCreation;
            public readonly string Source;

            public Entry(string eventLog, TimeSpan timeSinceCreation, string source)
            {
                EventLog = eventLog;
                TimeSinceCreation = timeSinceCreation;
                Source = source;
            }
        }
    }

    public class MultithreadSync : IDisposable
    {
        public class OwnerMismatchException : Exception
        {
            private readonly Owner releasingOwner;
            private readonly Owner firstInQueueOwner;

            public OwnerMismatchException(Owner releasingOwner, Owner firstInQueueOwner)
            {
                this.releasingOwner = releasingOwner;
                this.firstInQueueOwner = firstInQueueOwner;
            }

            public override string Message => $"Releasing owner {releasingOwner.Name} != Queue owner {firstInQueueOwner.Name}";
        }

        public class Owner
        {
            public readonly ManualResetEventSlim EventSlim = new (false);
            public readonly string Name;

            public Owner(string name)
            {
                Name = name;
            }
        }

        private static readonly CustomSampler SAMPLER;
        private static readonly TimeSpan MAX_LIMIT = TimeSpan.FromSeconds(10);

        private readonly ConcurrentQueue<Owner> queue = new ();
        private readonly Atomic<bool> acquired = new ();
        private readonly Atomic<bool> isDisposing = new ();

        private readonly SyncLogsBuffer syncLogsBuffer;

        public bool Acquired => acquired.Value();

        static MultithreadSync()
        {
            SAMPLER = CustomSampler.Create("MultithreadSync.Wait")!;
        }

        public MultithreadSync(SceneShortInfo sceneInfo)
        {
            syncLogsBuffer = new SyncLogsBuffer(sceneInfo, 20);
        }

        private void Acquire(Owner owner)
        {
            string source = owner.Name;

#if SYNC_DEBUG
            syncLogsBuffer.Report("MultithreadSync Acquire start for:", source);
#endif

            if (isDisposing.Value())
                throw new ObjectDisposedException(nameof(MultithreadSync));

            ManualResetEventSlim waiter = owner.EventSlim;
            bool shouldWait = queue.Count > 0;

            queue.Enqueue(owner);

            // There is already one thread doing work. Wait for the signal
            if (shouldWait && !waiter.Wait(MAX_LIMIT))
            {
                syncLogsBuffer.Print();

                throw new TimeoutException($"{nameof(MultithreadSync)} timeout, cannot acquire for: {source}");
            }

            acquired.Set(true);
#if SYNC_DEBUG
            syncLogsBuffer.Report("MultithreadSync Acquire finished for:", source);
#endif
        }

        private void Release(Owner owner)
        {
            string source = owner.Name;

#if SYNC_DEBUG
            syncLogsBuffer.Report("MultithreadSync Release start for:", source);
#endif

            if (isDisposing.Value())
                return;

            // If the queue is empty, then our logic is wrong
            if (queue.TryDequeue(out var finishedWaiter))
            {
                // The one releasing should be the one at the top of the queue
                if (owner != finishedWaiter)
                {
                    syncLogsBuffer.Print();
                    throw new OwnerMismatchException(owner, finishedWaiter);
                }

                finishedWaiter.EventSlim.Reset();
                acquired.Set(false);

                if (queue.TryPeek(out var next))
                    next.EventSlim.Set(); // Signal the next waiter in line

#if SYNC_DEBUG
                syncLogsBuffer.Report("MultithreadSync Release finished for:", source);
#endif
            }
#if SYNC_DEBUG
            else
                syncLogsBuffer.Report("MultithreadSync Release finished CANNOT", source);
#endif
        }

        public void Dispose()
        {
            isDisposing.Set(true);
            acquired.Set(false);

            foreach (Owner? ow in queue)
                ow.EventSlim.Reset();

            queue.Clear();
        }

        public Scope GetScope(Owner source)
        {
            SAMPLER.Begin();
            var scope = new Scope(this, source);
            SAMPLER.End();
            return scope;
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
                    throw new TimeoutException($"{nameof(MultithreadSync)} source {source} took too much time! cannot release for: {source}");
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
                scope = default;
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
