using System;
using System.Collections.Generic;
using System.Threading;
using DCL.Optimization.ThreadSafePool;
using UnityEngine.Profiling;

namespace Utility.Multithreading
{
    public class MultithreadSync : IDisposable
    {
        private static readonly CustomSampler SAMPLER;

        private static readonly ThreadSafeObjectPool<ManualResetEventSlim> MANUAL_RESET_EVENT_SLIM_POOL =
            new(() => new ManualResetEventSlim(false), actionOnRelease: e => e.Reset());

        private readonly object queueLock = new();
        private readonly Queue<ManualResetEventSlim> queue = new();
        private bool IsDisposing;

        public bool Acquired { get; private set; }

        static MultithreadSync()
        {
            SAMPLER = CustomSampler.Create("MultithreadSync.Wait");
        }

        public MultithreadSync()
        {
        }

        public void Acquire()
        {
            var waiter = MANUAL_RESET_EVENT_SLIM_POOL.Get();
            bool shouldWait;

            lock (queueLock)
            {
                shouldWait = queue.Count > 0;
                queue.Enqueue(waiter);
            }

            if (shouldWait)
                if (!waiter.Wait(TimeSpan.FromSeconds(10))) // There is already one thread doing work. Wait for the signal
                    throw new TimeoutException($"{nameof(MultithreadSync)} timeout");
            Acquired = true;
        }

        public void Release()
        {
            if (IsDisposing)
                return;

            lock (queueLock)
            {
                // The one releasing should be the one at the top of the queue
                // If the queue is empty, then our logic is wrong
                var finishedWaiter = queue.Dequeue();
                MANUAL_RESET_EVENT_SLIM_POOL.Release(finishedWaiter);
                Acquired = false;

                if (queue.Count > 0)
                    queue.Peek().Set(); // Signal the next waiter in line
            }
        }

        public void Dispose()
        {
            IsDisposing = true;
            lock (queueLock)
            {
                Acquired = false;
                foreach (var manualResetEventSlim in queue)
                    MANUAL_RESET_EVENT_SLIM_POOL.Release(manualResetEventSlim);
                queue.Clear();
            }
        }

        public Scope GetScope()
        {
            SAMPLER.Begin();
            var scope = new Scope(this);
            SAMPLER.End();
            return scope;
        }

        public readonly struct Scope : IDisposable
        {
            private readonly MultithreadSync multithreadSync;

            public Scope(MultithreadSync multithreadSync)
            {
                this.multithreadSync = multithreadSync;
                multithreadSync.Acquire();
            }

            public void Dispose()
            {
                multithreadSync.Release();
            }
        }
    }
}
