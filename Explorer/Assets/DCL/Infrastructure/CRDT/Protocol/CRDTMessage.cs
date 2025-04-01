using CRDT.Memory;
using System;
using System.Buffers;
using System.Runtime.InteropServices;

namespace CRDT.Protocol
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct CRDTMessage : IEquatable<CRDTMessage>
    {
        public CRDTMessage(CRDTMessageType type, CRDTEntity entityId, int componentId, int timestamp, IMemoryOwner<byte> data)
        {
            Type = type;
            EntityId = entityId;
            ComponentId = componentId;
            Timestamp = timestamp;
            Data = data ?? EmptyMemoryOwner<byte>.EMPTY;
        }

        public readonly CRDTMessageType Type;

        /// <summary>
        ///     Entity {
        ///     uint32_t id;
        ///     struct {
        ///     uint16_t version;
        ///     uint16_t number;
        ///     }
        /// </summary>
        public readonly CRDTEntity EntityId;

        public readonly int ComponentId;

        public readonly int Timestamp;

        /// <summary>
        ///     Using Memory provides additional versatility for pooling
        /// </summary>

        // The layout of this structure is not clear
        public readonly IMemoryOwner<byte> Data;

        public bool Equals(CRDTMessage other) =>
            Type == other.Type && EntityId.Equals(other.EntityId) && ComponentId == other.ComponentId && Timestamp == other.Timestamp && CRDTMessageComparer.CompareData(Data, other.Data) == 0;

        public override bool Equals(object obj) =>
            obj is CRDTMessage other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine((int)Type, EntityId, ComponentId, Timestamp, Data);

        public override string ToString() =>
            $"Type {Type}, Entity {EntityId}, Component {ComponentId}, Timestamp {Timestamp}, Data {Data.Memory.Length} Bytes";
    }
}
