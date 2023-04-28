using System;

namespace CRDT.Memory
{
    public interface IReadOnlyMemoryOwner<T> : IDisposable
    {
        public static readonly IReadOnlyMemoryOwner<T> EMPTY = new EmptyReadOnlyMemoryOwner<T>();

        ReadOnlyMemory<T> ReadOnlyMemory { get; }
    }

    internal class EmptyReadOnlyMemoryOwner<T> : IReadOnlyMemoryOwner<T>
    {
        public void Dispose() { }

        public ReadOnlyMemory<T> ReadOnlyMemory => ReadOnlyMemory<T>.Empty;
    }
}
