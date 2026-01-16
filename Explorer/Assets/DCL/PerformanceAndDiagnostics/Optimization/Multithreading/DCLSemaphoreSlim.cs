using System.Collections.Generic;
using Cysharp.Threading.Tasks;


namespace DCL.Optimization.Multithreading 
{
    public sealed class DCLSemaphoreSlim
    {
#if !UNITY_WEBGL
        private readonly System.Threading.SemaphoreSlim semaphore;
#else
        private readonly Queue<UniTaskCompletionSource> queue = new();
        private bool taken;
#endif

        public DCLSemaphoreSlim(int initialCount = 1)
        {
#if !UNITY_WEBGL
            semaphore = new System.Threading.SemaphoreSlim(initialCount, initialCount);
#else
            taken = initialCount == 0;
#endif
        }

        public UniTask WaitAsync()
        {
#if !UNITY_WEBGL
            return semaphore.WaitAsync().AsUniTask();
#else
            if (!taken)
            {
                taken = true;
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
                next.TrySetResult(); // resume FIFO
            }
            else
            {
                taken = false;
            }
#endif
        }
    }
}
