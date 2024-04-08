using System;
using System.Collections;
using System.Collections.Generic;

namespace CrdtEcsBridge.PoolsProviders
{
    public struct PoolableByteArray : IDisposable, IEnumerable<byte>
    {
        public static readonly PoolableByteArray EMPTY = new (System.Array.Empty<byte>(), 0, null);

        public readonly byte[] Array;
        public readonly int Length;
        public readonly ISharedPoolsProvider PoolsProvider;

        public PoolableByteArray(byte[] array, int length, ISharedPoolsProvider poolsProvider)
        {
            Array = array;
            Length = length;
            PoolsProvider = poolsProvider;
            IsDisposed = false;
        }

        public Span<byte> Span => new(Array, 0, Length);

        public bool IsDisposed { get; private set; }

        public bool IsEmpty => Length == 0;

        public void Dispose()
        {
            if (IsEmpty || IsDisposed) return;

            PoolsProvider.ReleaseSerializedStateBytesPool(Array);

            IsDisposed = true;
        }

        public IEnumerator<byte> GetEnumerator() =>
            Length <= 0 ? ((IEnumerable<byte>)System.Array.Empty<byte>()).GetEnumerator() : new Enumerator(this);

        IEnumerator IEnumerable.GetEnumerator() =>
            GetEnumerator();

        public struct Enumerator : IEnumerator<byte>
        {
            private readonly byte[] _array;
            private readonly int _end; // cache Offset + Count, since it's a little slow
            private int _current;

            internal Enumerator(PoolableByteArray arraySegment)
            {
                _array = arraySegment.Array;
                _end = arraySegment.Length;
                _current = -1;
            }

            public bool MoveNext()
            {
                if (_current < _end)
                {
                    _current++;
                    return _current < _end;
                }

                return false;
            }

            public byte Current
            {
                get
                {
                    if (_current < -1)
                        throw new InvalidOperationException("EnumNotStarted");

                    if (_current >= _end)
                        throw new InvalidOperationException("EnumEnded");

                    return _array[_current];
                }
            }

            object IEnumerator.Current => Current;

            void IEnumerator.Reset()
            {
                _current = -1;
            }

            public void Dispose() { }
        }
    }
}
