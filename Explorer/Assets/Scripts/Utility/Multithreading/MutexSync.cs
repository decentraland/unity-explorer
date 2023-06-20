using System;
using System.Threading;
using UnityEngine.Profiling;

namespace Utility.Multithreading
{
    public class MutexSync
    {
        private static readonly CustomSampler SAMPLER;

        private readonly Mutex mutex = new ();

        static MutexSync()
        {
            SAMPLER = CustomSampler.Create("MutexSync.Wait");
        }

        public Scope GetScope()
        {
            SAMPLER.Begin();
            var scope = new Scope(mutex);
            SAMPLER.End();
            return scope;
        }

        public readonly struct Scope : IDisposable
        {
            private readonly Mutex mutex;

            public Scope(Mutex mutex)
            {
                this.mutex = mutex;
                mutex.WaitOne();
            }

            public void Dispose()
            {
                mutex.ReleaseMutex();
            }
        }
    }
}
