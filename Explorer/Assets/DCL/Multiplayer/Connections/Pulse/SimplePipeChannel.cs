using Cysharp.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading;

namespace DCL.Multiplayer.Connections.Pulse
{
    public class SimplePipeChannel<T>
    {
        /// <summary>
        /// A minimal unbounded producer/consumer channel for net471,
        /// mimicking the subset of System.Threading.Channels used by MessagePipe.
        /// </summary>
        private readonly ConcurrentQueue<T> queue = new ();
        private readonly SemaphoreSlim signal = new (0);

        public bool TryWrite(T item)
        {
            queue.Enqueue(item);
            signal.Release();
            return true;
        }

        public bool TryRead(out T item) =>
            queue.TryDequeue(out item);

        public IUniTaskAsyncEnumerable<T> ReadAllAsync(CancellationToken ct) =>
            new ChannelAsyncEnumerable(this, ct);

        private async UniTask<T> ReadAsync(CancellationToken ct)
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                await signal.WaitAsync(ct);

                if (queue.TryDequeue(out T item))
                    return item;
            }
        }

        private sealed class ChannelAsyncEnumerable : IUniTaskAsyncEnumerable<T>
        {
            private readonly SimplePipeChannel<T> channel;
            private readonly CancellationToken ct;

            public ChannelAsyncEnumerable(SimplePipeChannel<T> channel, CancellationToken ct)
            {
                this.channel = channel;
                this.ct = ct;
            }

            public IUniTaskAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken)
            {
                var token = cancellationToken.CanBeCanceled ? cancellationToken : ct;
                return new ChannelAsyncEnumerator(channel, token);
            }
        }

        private sealed class ChannelAsyncEnumerator : IUniTaskAsyncEnumerator<T>
        {
            private readonly SimplePipeChannel<T> channel;
            private readonly CancellationToken ct;

            public T Current { get; private set; }

            public ChannelAsyncEnumerator(SimplePipeChannel<T> channel, CancellationToken ct)
            {
                this.channel = channel;
                this.ct = ct;
            }

            public async UniTask<bool> MoveNextAsync()
            {
                if (ct.IsCancellationRequested)
                    return false;

                try
                {
                    Current = await channel.ReadAsync(ct);
                    return true;
                }
                catch (System.OperationCanceledException)
                {
                    return false;
                }
            }

            public UniTask DisposeAsync() => UniTask.CompletedTask;
        }
    }
}
