using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Profiling;

namespace Utility.Multithreading
{
    public class MutexSync : IDisposable
    {
        private static readonly CustomSampler SAMPLER;

        private readonly object _lock = new();
        private readonly Queue<ManualResetEventSlim> _queue = new();

        public bool Acquired { get; private set; }

        static MutexSync()
        {
            SAMPLER = CustomSampler.Create("MutexSync.Wait");
        }

        public void Acquire()
        {
            var waiter = new ManualResetEventSlim(false);

            lock (_lock)
            {
                _queue.Enqueue(waiter);
                if (_queue.Count == 1)
                    waiter.Set(); // If there is only one item in the queue, signal it right away so Wait() is ignored
            }

            waiter.Wait();
            Acquired = true;
        }

        public void Release()
        {
            lock (_lock)
            {
                // The one releasing should be the one at the top of the queue
                // If the queue is empty, then our logic is wrong
                var finishedWaiter = _queue.Dequeue();
                finishedWaiter.Dispose(); // Clean up the finished waiter
                Acquired = false;
                
                if (_queue.Count > 0)
                    _queue.Peek().Set(); // Signal the next waiter in line
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                Acquired = false;
                foreach (var manualResetEventSlim in _queue)
                    manualResetEventSlim.Dispose(); // Clean up waiters. Nothing to do here
                _queue.Clear();
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
            private readonly MutexSync mutex;

            public Scope(MutexSync mutex)
            {
                this.mutex = mutex;
                mutex.Acquire();
            }

            public void Dispose()
            {
                mutex.Release();
            }
        }
    }
}
