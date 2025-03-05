using System;

namespace DCL.Optimization.Memory
{
    public interface IMemoryIterator : IDisposable
    {
        ReadOnlyMemory<byte> Current { get; }

        int? TotalSize { get; }

        bool MoveNext();
    }

}
