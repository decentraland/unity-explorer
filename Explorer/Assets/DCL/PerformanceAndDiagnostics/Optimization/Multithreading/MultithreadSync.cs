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

                circularBuffer.AddLast(new Entry(eventLog, DateTime.Now - creationTime, source, Thread.CurrentThread.ManagedThreadId));
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
        private static readonly CustomSampler SAMPLER;
        private static readonly TimeSpan MAX_LIMIT = TimeSpan.FromSeconds(10);

        private static readonly ThreadSafeObjectPool<ManualResetEventSlim> MANUAL_RESET_EVENT_SLIM_POOL =
            new (() => new ManualResetEventSlim(false));

        private readonly ConcurrentQueue<ManualResetEventSlim> queue = new ();
        private readonly Atomic<bool> acquired = new ();
        private readonly Atomic<bool> isDisposing = new ();
        private long acquireCount;

        private readonly SyncLogsBuffer syncLogsBuffer;
        private readonly Atomic<(string? name, DateTime startedAt, long accuiredIndex)> currentScope = new ((null, DateTime.MinValue, 0));

        public bool Acquired => acquired.Value();

        static MultithreadSync()
        {
            SAMPLER = CustomSampler.Create("MultithreadSync.Wait")!;
        }

        public MultithreadSync(SceneShortInfo sceneInfo)
        {
            syncLogsBuffer = new SyncLogsBuffer(sceneInfo, 20);
        }

        private void Acquire(string source)
        {
#if SYNC_DEBUG
            syncLogsBuffer.Report("MultithreadSync Acquire start for:", source);
#endif
            Interlocked.Increment(ref acquireCount);
            long currentId = Interlocked.Read(ref acquireCount);

            if (isDisposing.Value())
                throw new ObjectDisposedException(nameof(MultithreadSync));

            var waiter = MANUAL_RESET_EVENT_SLIM_POOL.Get();
            bool shouldWait = queue.Count > 0;

            queue.Enqueue(waiter);

            // There is already one thread doing work. Wait for the signal
            if (shouldWait && !waiter.Wait(MAX_LIMIT))
            {
                var time = DateTime.Now;
                var current = currentScope.Value();
                var difference = time - current.startedAt;
                syncLogsBuffer.Print();

                throw new TimeoutException($"{nameof(MultithreadSync)} timeout, cannot acquire for: {source}, main context \"{current.name}\" id \"{current.accuiredIndex}\" takes too long: {difference.TotalSeconds}");
            }

            acquired.Set(true);
#if SYNC_DEBUG
            syncLogsBuffer.Report("MultithreadSync Acquire finished for:", source);
#endif
            currentScope.Set((source, DateTime.Now, currentId));
        }

        private void Release(string source)
        {
#if SYNC_DEBUG
            syncLogsBuffer.Report("MultithreadSync Release start for:", source);
#endif

            if (isDisposing.Value())
                return;

            // The one releasing should be the one at the top of the queue
            // If the queue is empty, then our logic is wrong
            if (queue.TryDequeue(out var finishedWaiter))
            {
                finishedWaiter!.Reset();
                MANUAL_RESET_EVENT_SLIM_POOL.Release(finishedWaiter);
                acquired.Set(false);

                if (queue.TryPeek(out var next))
                    next!.Set(); // Signal the next waiter in line

#if SYNC_DEBUG
                syncLogsBuffer.Report("MultithreadSync Release finished for:", source);
#endif
            }
#if SYNC_DEBUG
            else
                syncLogsBuffer.Report("MultithreadSync Release finished CANNOT", source);
#endif
            currentScope.Set((null, DateTime.MinValue, 0));
        }

        public void Dispose()
        {
            isDisposing.Set(true);
            acquired.Set(false);

            foreach (var manualResetEventSlim in queue)
                MANUAL_RESET_EVENT_SLIM_POOL.Release(manualResetEventSlim);

            queue.Clear();
        }

        public Scope GetScope(string source)
        {
            SAMPLER.Begin();
            var scope = new Scope(this, source);
            SAMPLER.End();
            return scope;
        }

        public readonly struct Scope : IDisposable
        {
            private readonly MultithreadSync multithreadSync;
            private readonly string source;
            private readonly DateTime start;

            public Scope(MultithreadSync multithreadSync, string source)
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

            public void Acquire(string source)
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
