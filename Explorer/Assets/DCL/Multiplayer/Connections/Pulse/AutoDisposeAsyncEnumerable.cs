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
        private readonly Action onDispose;

        public AutoDisposeAsyncEnumerable(IUniTaskAsyncEnumerable<T> source, Action onDispose)
        {
            this.source = source;
            this.onDispose = onDispose;
        }

        public IUniTaskAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
            new Enumerator(source.GetAsyncEnumerator(cancellationToken), onDispose);

        private sealed class Enumerator : IUniTaskAsyncEnumerator<T>
        {
            private readonly IUniTaskAsyncEnumerator<T> inner;
            private readonly Action onDispose;
            private bool hasCurrent;

            public T Current => inner.Current;

            public Enumerator(IUniTaskAsyncEnumerator<T> inner, Action onDispose)
            {
                this.inner = inner;
                this.onDispose = onDispose;
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

                onDispose();
                return inner.DisposeAsync();
            }
        }
    }
}
