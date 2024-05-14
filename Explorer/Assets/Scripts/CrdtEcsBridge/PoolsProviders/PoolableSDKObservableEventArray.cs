using SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents.Events;
using System;
using System.Collections;
using System.Collections.Generic;

namespace CrdtEcsBridge.PoolsProviders
{
    public struct PoolableSDKObservableEventArray : IDisposable, IEnumerable<SDKObservableEvent>
    {
        public readonly SDKObservableEvent[] Array;
        public readonly Action<SDKObservableEvent[]> ReleaseFunc;

        public PoolableSDKObservableEventArray(SDKObservableEvent[] array, int length, Action<SDKObservableEvent[]> releaseFunc)
        {
            Array = array;
            Length = length;
            ReleaseFunc = releaseFunc;
            IsDisposed = false;
        }

        public int Length { get; private set; }

        public bool IsDisposed { get; private set; }

        public bool IsEmpty => Length == 0;

        public void SetLength(int length)
        {
            if (length > Array.Length)
                throw new ArgumentOutOfRangeException(nameof(length), $"Rented Array Size {Array.Length} is lower than the requested {length}");

            Length = length;
        }

        public void Dispose()
        {
            if (IsEmpty || IsDisposed) return;

            ReleaseFunc(Array);

            IsDisposed = true;
        }

        public IEnumerator<SDKObservableEvent> GetEnumerator() =>
            Length <= 0 ? ((IEnumerable<SDKObservableEvent>)System.Array.Empty<SDKObservableEvent>()).GetEnumerator() : new Enumerator(this);

        IEnumerator IEnumerable.GetEnumerator() =>
            GetEnumerator();

        public struct Enumerator : IEnumerator<SDKObservableEvent>
        {
            private readonly SDKObservableEvent[] _array;
            private readonly int _end; // cache Offset + Count, since it's a little slow
            private int _current;

            internal Enumerator(PoolableSDKObservableEventArray arraySegment)
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

            public SDKObservableEvent Current
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
