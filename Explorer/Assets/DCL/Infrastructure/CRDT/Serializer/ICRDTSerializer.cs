using CRDT.Protocol.Factory;
using System;

namespace CRDT.Serializer
{
    public interface ICRDTSerializer
    {
        void Serialize(ref Span<byte> destination, in ProcessedCRDTMessage processedMessage);
    }
}
