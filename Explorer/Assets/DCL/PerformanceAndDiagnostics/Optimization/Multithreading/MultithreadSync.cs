using DCL.Diagnostics;
using System;
using System.Threading;
using DCL.Optimization.ThreadSafePool;
using System.Collections.Concurrent;
using UnityEngine.Profiling;

namespace Utility.Multithreading
{
    public class MultithreadSync : IDisposable
    {
        private static readonly CustomSampler SAMPLER;
        private static readonly TimeSpan MAX_LIMIT = TimeSpan.FromSeconds(10);

        private static readonly ThreadSafeObjectPool<ManualResetEventSlim> MANUAL_RESET_EVENT_SLIM_POOL =
            new (() => new ManualResetEventSlim(false));

        private readonly ConcurrentQueue<ManualResetEventSlim> queue = new ();
        private readonly Atomic<bool> acquired = new ();
        private readonly Atomic<bool> isDisposing = new ();

        public bool Acquired => acquired.Value();

        static MultithreadSync()
        {
            SAMPLER = CustomSampler.Create("MultithreadSync.Wait")!;
        }

        private void Acquire(string source, SceneShortInfo sceneShortInfo)
        {
#if SYNC_DEBUG
            var reportData = new ReportData(ReportCategory.SYNC, sceneShortInfo: sceneShortInfo);
            ReportHub.Log(reportData, $"MultithreadSync Acquire start for: {source}");
#endif

            if (isDisposing.Value())
                throw new ObjectDisposedException(nameof(MultithreadSync));

            var waiter = MANUAL_RESET_EVENT_SLIM_POOL.Get();
            bool shouldWait = queue.Count > 0;

            queue.Enqueue(waiter);

            if (shouldWait && !waiter.Wait(MAX_LIMIT))

                // There is already one thread doing work. Wait for the signal
                throw new TimeoutException($"{nameof(MultithreadSync)} timeout, cannot acquire for: {source}");

            acquired.Set(true);
#if SYNC_DEBUG
            ReportHub.Log(reportData, $"MultithreadSync Acquire finished for: {source}");
#endif
        }

        private void Release(string source, SceneShortInfo sceneShortInfo)
        {
#if SYNC_DEBUG
            var reportData = new ReportData(ReportCategory.SYNC, sceneShortInfo: sceneShortInfo);
            ReportHub.Log(reportData, $"MultithreadSync Release start for: {source}");
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
                ReportHub.Log(reportData, $"MultithreadSync Release finished for: {source}");
#endif
            }
#if SYNC_DEBUG
            else
                ReportHub.LogError(reportData, $"MultithreadSync Release finished CANNOT: {source}");
#endif
        }

        public void Dispose()
        {
            isDisposing.Set(true);
            acquired.Set(false);

            foreach (var manualResetEventSlim in queue)
                MANUAL_RESET_EVENT_SLIM_POOL.Release(manualResetEventSlim);

            queue.Clear();
        }

        public Scope GetScope(string source, SceneShortInfo sceneShortInfo)
        {
            SAMPLER.Begin();
            var scope = new Scope(this, source, sceneShortInfo);
            SAMPLER.End();
            return scope;
        }

        public readonly struct Scope : IDisposable
        {
            private readonly MultithreadSync multithreadSync;
            private readonly string source;
            private readonly SceneShortInfo sceneShortInfo;
            private readonly DateTime start;

            public Scope(MultithreadSync multithreadSync, string source, SceneShortInfo sceneShortInfo)
            {
                this.multithreadSync = multithreadSync;
                this.source = source;
                this.sceneShortInfo = sceneShortInfo;
                multithreadSync.Acquire(source, this.sceneShortInfo);
                start = DateTime.Now;
            }

            public void Dispose()
            {
                if (DateTime.Now - start > MAX_LIMIT)
                    throw new TimeoutException($"{nameof(MultithreadSync)} source {source} took too much time! cannot release for: {source}");

                multithreadSync.Release(source, sceneShortInfo);
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

            public void Acquire(string source, SceneShortInfo sceneShortInfo)
            {
                scope = multithreadSync.GetScope(source, sceneShortInfo);
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
