using Cysharp.Threading.Tasks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Utility.Types;

namespace Plugins.TexturesFuse.TexturesServerWrap.Unzips
{
    public class PooledTexturesUnzip : ITexturesUnzip
    {
        private readonly IReadOnlyList<ITexturesUnzip> uniqueUnzips;
        private readonly TimeSpan timeout = TimeSpan.FromSeconds(15);
        private readonly ConcurrentBag<ITexturesUnzip> workers;

        public PooledTexturesUnzip(Func<ITexturesUnzip> ctor, int count) : this(
            Enumerable.Range(0, count).Select(_ => ctor()).ToArray()
        ) { }

        public PooledTexturesUnzip(IReadOnlyList<ITexturesUnzip> uniqueUnzips)
        {
            this.uniqueUnzips = uniqueUnzips;
            workers = new ConcurrentBag<ITexturesUnzip>(uniqueUnzips);
        }

        public async UniTask<EnumResult<OwnedTexture2D, NativeMethods.ImageResult>> TextureFromBytesAsync(IntPtr bytes, int bytesLength, TextureType type, CancellationToken token)
        {
            using var workerScope = await WorkerScope.NewWorkerScopeAsync(workers, timeout);
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

            public readonly ITexturesUnzip Worker;
            private readonly ConcurrentBag<ITexturesUnzip> workers;

            private WorkerScope(ConcurrentBag<ITexturesUnzip> workers, ITexturesUnzip worker)
            {
                this.workers = workers;
                this.Worker = worker;
            }

            public static async UniTask<WorkerScope> NewWorkerScopeAsync(ConcurrentBag<ITexturesUnzip> workers, TimeSpan timeout)
            {
                var startTime = DateTime.UtcNow;
                ITexturesUnzip worker;

                while (workers.TryTake(out worker) == false)
                {
                    if (DateTime.UtcNow - startTime > timeout)
                        throw new TimeoutException();

                    await UniTask.Delay(POLL_DELAY);
                }

                return new WorkerScope(workers, worker!);
            }

            public void Dispose()
            {
                workers.Add(Worker);
            }
        }
    }
}
