using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace Utility.Multithreading
{
    public class MutexSlim<T> : IDisposable where T: IDisposable
    {
        private readonly SemaphoreSlim semaphoreSlim = new (1, 1);
        private readonly T resource;
        public bool Disposed { get; private set; }

        public MutexSlim(T resource)
        {
            this.resource = resource;
        }

        public async UniTask AccessAsync(Action<T> action, CancellationToken token = default)
        {
            await semaphoreSlim.WaitAsync(token);

            try { action(resource); }
            finally { semaphoreSlim.Release(); }
        }

        public async UniTask AccessAsync<TCtx>(TCtx ctx, Action<T, TCtx> action, CancellationToken token = default)
        {
            await semaphoreSlim.WaitAsync(token);

            try { action(resource, ctx); }
            finally { semaphoreSlim.Release(); }
        }

        public async UniTask AccessAsync<TCtx>(TCtx ctx, Func<T, TCtx, UniTask> action, CancellationToken token = default)
        {
            await semaphoreSlim.WaitAsync(token);

            try { await action(resource, ctx); }
            finally { semaphoreSlim.Release(); }
        }

        public async UniTask<Tk> AccessAsync<Tk>(Func<T, UniTask<Tk>> action, CancellationToken token = default)
        {
            await semaphoreSlim.WaitAsync(token);

            try { return await action(resource); }
            finally { semaphoreSlim.Release(); }
        }

        public Tk Access<Tk>(Func<T, Tk> action, CancellationToken token = default)
        {
            semaphoreSlim.Wait(token);

            try { return action(resource); }
            finally { semaphoreSlim.Release(); }
        }

        public void Dispose()
        {
            try
            {
                semaphoreSlim.Wait();
                resource.Dispose();
                Disposed = true;
            }
            finally
            {
                semaphoreSlim.Release();
                semaphoreSlim.Dispose();
            }
        }
    }
}
