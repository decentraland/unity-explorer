// TRUST_WEBGL_THREAD_SAFETY_FLAG
// DCLSemaphoreSlim is designed as WebGL / Desktop friendly

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Utility.Multithreading
{
    public sealed class DCLSemaphoreSlim
    {
#if !UNITY_WEBGL
        private readonly System.Threading.SemaphoreSlim semaphore;
#else
        private readonly Queue<UniTaskCompletionSource> queue = new();
        private int count;
        private readonly int maxCount;
        private bool disposed;
#endif

        public DCLSemaphoreSlim(int initialCount = 1, int maxCount = 1)
        {
            if (initialCount < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCount));
            if (maxCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxCount));
            if (initialCount > maxCount)
                throw new ArgumentException("initialCount cannot be greater than maxCount");

#if !UNITY_WEBGL
            semaphore = new System.Threading.SemaphoreSlim(initialCount, maxCount);
#else
            this.count = initialCount;
            this.maxCount = maxCount;
#endif
        }
        
        public void Dispose()
        {
#if !UNITY_WEBGL
            semaphore.Dispose();
#else
            if (disposed)
                return;

            disposed = true;

            while (queue.Count > 0)
            {
                var waiter = queue.Dequeue();
                waiter.TrySetCanceled();
            }
#endif
        }

        public UniTask WaitAsync()
        {
#if !UNITY_WEBGL
            return semaphore.WaitAsync().AsUniTask();
#else
            if (count > 0)
            {
                count--;
                return UniTask.CompletedTask;
            }

            var ucs = new UniTaskCompletionSource();
            queue.Enqueue(ucs);
            return ucs.Task;
#endif        
        }

        public void Release()
        {
#if !UNITY_WEBGL
            semaphore.Release();
#else
            if (queue.Count > 0)
            {
                var next = queue.Dequeue();
                next.TrySetResult(); // FIFO resume
            }
            else
            {
                if (count == maxCount)
                    throw new System.Threading.SemaphoreFullException();

                count++;
            }
#endif        
        }
    }
}
