using Microsoft.ClearScript.V8.FastProxy;
using System;

namespace CrdtEcsBridge.PoolsProviders
{
    public struct PoolableByteArray : IDisposable, IV8FastHostObject
    {
        public static readonly PoolableByteArray EMPTY = new (System.Array.Empty<byte>(), 0, null);

        public readonly byte[] Array;
        private Action<byte[]>? releaseFunc;

        private static readonly V8FastHostObjectOperations<PoolableByteArray>
            OPERATIONS = new ();

        IV8FastHostObjectOperations IV8FastHostObject.Operations => OPERATIONS;

        static PoolableByteArray()
        {
            OPERATIONS.Configure(static configuration =>
            {
                configuration.SetEnumeratorFactory(
                    static self => new Enumerator(self));
            });
        }

        public PoolableByteArray(byte[] array, int length, Action<byte[]>? releaseFunc)
        {
            Array = array;
            Length = length;
            this.releaseFunc = releaseFunc;
        }

        public int Length { get; private set; }

        public Span<byte> Span => new(Array, 0, Length);

        public Memory<byte> Memory => Array.AsMemory(0, Length);

        public bool IsEmpty => Length == 0;

        public void SetLength(int length)
        {
            if (length > Array.Length)
                throw new ArgumentOutOfRangeException(nameof(length),
                    $"Rented Array Size {Array.Length} is lower than the requested {length}");

            Length = length;
        }

        public void Dispose()
        {
            if (releaseFunc != null)
            {
                releaseFunc(Array);
                releaseFunc = null;
            }
        }

        private sealed class Enumerator : IV8FastEnumerator
        {
            private readonly byte[] array;
            private readonly int end; // cache Offset + Count, since it's a little slow
            private int current;

            public Enumerator(PoolableByteArray arraySegment)
            {
                array = arraySegment.Array;
                end = arraySegment.Length;
                current = -1;
            }

            public void Dispose() { }

            public void GetCurrent(in V8FastResult item) =>
                item.Set(array[current]);

            public bool MoveNext() =>
                ++current < end;
        }
    }
}
