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

        public bool Acquired { get; private set; }

        static MultithreadSync()
        {
            SAMPLER = CustomSampler.Create("MutexSync.Wait");
        }

        public void Acquire()
        {
            var waiter = MANUAL_RESET_EVENT_SLIM_POOL.Get();

            lock (queueLock)
                queue.Enqueue(waiter);

            if (queue.Count > 1)
                waiter.Wait(); // There is already one thread doing work. Wait for the signal 
            Acquired = true;
        }

        public void Release()
        {
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