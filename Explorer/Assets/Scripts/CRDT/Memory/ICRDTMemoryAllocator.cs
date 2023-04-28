using System;

namespace CRDT.Memory
{
    /// <summary>
    /// The abstraction controls how byte arrays for CRDT Messages are retrieved and released.
    /// This scheme is not used yet as the life cycle of the data being passed to EntityComponentData and CRDTMessages is not straightforward
    /// and requires further investigation
    /// </summary>
    public interface ICRDTMemoryAllocator
    {
        IReadOnlyMemoryOwner<byte> GetMemoryBuffer(in ReadOnlyMemory<byte> originalStream, int shift, int length);
    }
}
