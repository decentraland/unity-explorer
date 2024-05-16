using DCL.Diagnostics;
using System;
using System.Threading;
using UnityEngine.Profiling;

namespace Utility.Multithreading
{
    public class MutexSync : IDisposable
    {
        private static readonly CustomSampler SAMPLER;
        private const int TIMEOUT = 1000;

        private readonly Mutex mutex = new ();

        public bool Acquired { get; private set; }

        static MutexSync()
        {
            SAMPLER = CustomSampler.Create("MutexSync.Wait");
        }

        public void Acquire()
        {
            if (mutex.WaitOne(TIMEOUT))
            {
                Acquired = true;
            }
            else
            {
                ReportHub.LogWarning(ReportCategory.ENGINE, $"MutexSync.Acquire: Failed to acquire mutex in the timeout {TIMEOUT}ms.");
            }

        }

        public void Release()
        {
            if(Acquired) mutex.ReleaseMutex();
            Acquired = false;
        }

        public void Dispose()
        {
            Acquired = false;
            mutex.Dispose();
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
