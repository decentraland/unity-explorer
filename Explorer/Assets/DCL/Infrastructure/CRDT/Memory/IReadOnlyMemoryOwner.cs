using System;
using System.Buffers;

namespace CRDT.Memory
{
    public class EmptyMemoryOwner<T> : IMemoryOwner<T>
    {
        public static readonly IMemoryOwner<T> EMPTY = new EmptyMemoryOwner<T>();

        public Memory<T> Memory => Memory<T>.Empty;

        public void Dispose() { }
    }
}
