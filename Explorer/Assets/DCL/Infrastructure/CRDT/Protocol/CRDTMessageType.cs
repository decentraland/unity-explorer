// ReSharper disable InconsistentNaming
using System;
using Utility;

namespace CRDT.Protocol
{
    public enum CRDTMessageType : byte
    {
        NONE = 0,

        // The last component per type is valid, others can be discarded
        PUT_COMPONENT = 1,
        DELETE_COMPONENT = 2,
        DELETE_ENTITY = 3,

        // All components are valid and must be processed
        APPEND_COMPONENT = 4,

        // Network
        PUT_COMPONENT_NETWORK = 5,
        DELETE_COMPONENT_NETWORK = 6,
        DELETE_ENTITY_NETWORK = 7,

        MAX_MESSAGE_TYPE,
    }

    public static class CRDTMessageTypeUtils
    {
        /// <summary>
        /// Component header + variable (if appliable), input is after general header
        /// </summary>
        public static uint TypeLengthBytes(CRDTMessageType type, ReadOnlySpan<byte> memory) =>
            type switch
            {
                CRDTMessageType.NONE => 0,
                CRDTMessageType.PUT_COMPONENT => 16 + memory.Slice(12).ReadConst<uint>(), // 16 bytes - header, 12 bytes - dataLength offset
                CRDTMessageType.DELETE_COMPONENT => 12, // 12 bytes - header
                CRDTMessageType.DELETE_ENTITY => 4, // 4 bytes - header (entity)
                CRDTMessageType.APPEND_COMPONENT => 16 + memory.Slice(12).ReadConst<uint>(), // 16 bytes - header, 12 bytes - dataLength offset
                CRDTMessageType.PUT_COMPONENT_NETWORK => 20 + memory.Slice(16).ReadConst<uint>(), // 20 bytes - header, 16 bytes - dataLength offset
                CRDTMessageType.DELETE_COMPONENT_NETWORK => 16, // header
                CRDTMessageType.DELETE_ENTITY_NETWORK => 8, // header
                CRDTMessageType.MAX_MESSAGE_TYPE => 0, // header
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };

    }
}
