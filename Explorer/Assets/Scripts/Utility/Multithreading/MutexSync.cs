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

        private readonly object _queueLock = new();
        private readonly Queue<Thread> _queue = new();
        private readonly object _threadLock = new();

        public bool Acquired { get; private set; }

        static MutexSync()
        {
            SAMPLER = CustomSampler.Create("MutexSync.Wait");
        }

        public void Acquire()
        {
            lock (_queueLock)
            {
                _queue.Enqueue(Thread.CurrentThread);
            }

            while (true)
            {
                lock (_queueLock)
                {
                    if (_queue.Peek() == Thread.CurrentThread)
                    {
                        break; // The current thread is at the front of the queue
                    }
                }

                lock (_threadLock)
                {
                    Monitor.Wait(_threadLock); // Wait until notified
                }
            }

            Acquired = true;
            Debug.Log($"JUANI LOCK ACQUIRED! {Thread.CurrentThread.Name ?? "Unnamed Thread"}");
        }

        public void Release()
        {
            Debug.Log("JUANI RELEASING LOCK!");
            Acquired = false;

            lock (_queueLock)
            {
                _queue.Dequeue(); // Remove the current thread from the queue

                if (_queue.Count > 0)
                {
                    lock (_threadLock)
                    {
                        Monitor.PulseAll(_threadLock); // Notify other waiting threads
                    }
                }
            }
        }

        public void Dispose()
        {
            Acquired = false;
            lock (_queueLock)
                _queue.Clear();
            lock (_threadLock)
            {
                Monitor.PulseAll(_threadLock); // Notify other waiting threads
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
