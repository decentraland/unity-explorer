using System;
using System.Threading;
using UnityEngine.Profiling;

namespace Utility.Multithreading
{
    public class MutexSync : IDisposable
    {
        private static readonly CustomSampler SAMPLER;

        public readonly Mutex Mutex = new ();

        static MutexSync()
        {
            SAMPLER = CustomSampler.Create("MutexSync.Wait");
        }

        public void Dispose()
        {
            Mutex.Dispose();
        }

        public Scope GetScope()
        {
            SAMPLER.Begin();
            var scope = new Scope(Mutex);
            SAMPLER.End();
            return scope;
        }

        public readonly struct Scope : IDisposable
        {
            private readonly Mutex mutex;

            public Scope(Mutex mutex)
            {
                this.mutex = mutex;
                mutex.WaitOne(10000);
            }

            public void Dispose()
            {
                mutex.ReleaseMutex();
            }
        }
    }
}
