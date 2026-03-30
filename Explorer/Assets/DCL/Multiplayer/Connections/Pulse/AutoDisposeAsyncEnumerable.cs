using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.Multiplayer.Connections.Pulse
{
    /// <summary>
    ///     Wraps an async enumerable of disposable items so that each item is automatically
    ///     disposed when the next one is retrieved or when the enumerator is disposed.
    ///     This prevents pool leaks when consumers use implicit conversion and never call Dispose manually.
    /// </summary>
    internal readonly struct AutoDisposeAsyncEnumerable<T> : IUniTaskAsyncEnumerable<T> where T: IDisposable
    {
        private readonly IUniTaskAsyncEnumerable<T> source;

        public AutoDisposeAsyncEnumerable(IUniTaskAsyncEnumerable<T> source)
        {
            this.source = source;
        }

        public IUniTaskAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
            new Enumerator(source.GetAsyncEnumerator(cancellationToken));

        private sealed class Enumerator : IUniTaskAsyncEnumerator<T>
        {
            private readonly IUniTaskAsyncEnumerator<T> inner;
            private bool hasCurrent;

            public T Current => inner.Current;

            public Enumerator(IUniTaskAsyncEnumerator<T> inner)
            {
                this.inner = inner;
            }

            public async UniTask<bool> MoveNextAsync()
            {
                if (hasCurrent)
                    inner.Current.Dispose();

                bool moved = await inner.MoveNextAsync();
                hasCurrent = moved;
                return moved;
            }

            public UniTask DisposeAsync()
            {
                if (hasCurrent)
                {
                    inner.Current.Dispose();
                    hasCurrent = false;
                }

                return inner.DisposeAsync();
            }
        }
    }
}
