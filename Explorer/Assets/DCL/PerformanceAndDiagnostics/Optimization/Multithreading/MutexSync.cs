using System;
using System.Threading;
using UnityEngine.Profiling;

namespace DCL.Optimization.Multithreading
{
    public class MutexSync : IDisposable
    {
        private static readonly CustomSampler SAMPLER;

#if !UNITY_WEBGL
        private readonly Mutex mutex = new (); // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG
#endif

        public bool Acquired { get; private set; }

        static MutexSync()
        {
            SAMPLER = CustomSampler.Create("MutexSync.Wait");
        }

        public void Acquire()
        {
#if !UNITY_WEBGL
            mutex.WaitOne();
#endif
            Acquired = true;
        }

        public void Release()
        {
#if !UNITY_WEBGL
            mutex.ReleaseMutex();
#endif
            Acquired = false;
        }

        public void Dispose()
        {
            Acquired = false;
#if !UNITY_WEBGL
            mutex.Dispose();
#endif
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
