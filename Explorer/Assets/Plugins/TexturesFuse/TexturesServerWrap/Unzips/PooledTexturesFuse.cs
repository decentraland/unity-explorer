using Cysharp.Threading.Tasks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Utility.Multithreading;
using Utility.Types;

namespace Plugins.TexturesFuse.TexturesServerWrap.Unzips
{
    internal class PooledTexturesFuse : ITexturesFuse
    {
        private readonly IReadOnlyList<ITexturesFuse> uniqueUnzips;
        private readonly TimeSpan timeout = TimeSpan.FromSeconds(70);
        private readonly ConcurrentQueue<ITexturesFuse> workers;

        public PooledTexturesFuse(Func<ITexturesFuse> ctor, int count) : this(
            Enumerable.Range(0, count).Select(_ => ctor()).ToArray()
        ) { }

        public PooledTexturesFuse(IReadOnlyList<ITexturesFuse> uniqueUnzips)
        {
            this.uniqueUnzips = uniqueUnzips;
            workers = new ConcurrentQueue<ITexturesFuse>(uniqueUnzips);
        }

        public async UniTask<EnumResult<IOwnedTexture2D, NativeMethods.ImageResult>> TextureFromBytesAsync(IntPtr bytes, int bytesLength, TextureType type, CancellationToken token)
        {
            using var workerScope = await WorkerScope.NewWorkerScopeAsync(workers, timeout, token);
            return await workerScope.Worker.TextureFromBytesAsync(bytes, bytesLength, type, token);
        }

        public void Dispose()
        {
            foreach (var unzip in uniqueUnzips)
                unzip.Dispose();
        }

        private readonly struct WorkerScope : IDisposable
        {
            private static readonly TimeSpan POLL_DELAY = TimeSpan.FromMilliseconds(50);
            private static readonly Atomic<DateTime> SHARED_START_TIME = new (DateTime.UtcNow);

            public readonly ITexturesFuse Worker;
            private readonly ConcurrentQueue<ITexturesFuse> workers;

            private WorkerScope(ConcurrentQueue<ITexturesFuse> workers, ITexturesFuse worker)
            {
                this.workers = workers;
                this.Worker = worker;
            }

            public static async UniTask<WorkerScope> NewWorkerScopeAsync(ConcurrentQueue<ITexturesFuse> workers, TimeSpan timeout, CancellationToken token)
            {
                SHARED_START_TIME.Set(DateTime.UtcNow);
                ITexturesFuse worker;

                while (workers.TryDequeue(out worker) == false)
                {
                    if (DateTime.UtcNow - SHARED_START_TIME.Value() > timeout)
                        throw new TimeoutException();

                    await UniTask.Delay(POLL_DELAY, cancellationToken: token);
                }

                return new WorkerScope(workers, worker!);
            }

            public void Dispose()
            {
                workers.Enqueue(Worker);
            }
        }
    }
}
