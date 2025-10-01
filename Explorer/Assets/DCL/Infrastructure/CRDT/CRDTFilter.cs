#nullable enable

using CRDT.Protocol;
using System;
using Utility;

namespace CRDT
{
    public class CRDTFilter
    {
        private static readonly uint NO_SYNC_COMPONENT_ID = 2092194694;

        /// <summary>
        /// Output must be equal or bigger than memory
        /// </summary>
        public static void FilterSceneMessageBatch(ReadOnlySpan<byte> memory, Span<byte> output, out int totalWrite)
        {
            totalWrite = 0;

            // write first - byte, it's for
            const int CRDT_STATE_LENGTH = 1;
            totalWrite += CRDT_STATE_LENGTH;
            memory.Slice(0, CRDT_STATE_LENGTH).CopyTo(output);
            memory = memory.Slice(CRDT_STATE_LENGTH);
            output = output.Slice(CRDT_STATE_LENGTH);

            // While we have a header to read
            while (memory.Length > CRDTConstants.MESSAGE_HEADER_LENGTH)
            {
                uint messageLength = memory.Read<uint>();
                CRDTMessageType messageType = memory.ReadEnumAs<CRDTMessageType, uint>();

                // Message length lower than minimal, it's an invalid message
                if (messageLength <= CRDTConstants.MESSAGE_HEADER_LENGTH)
                    break;

                // Do we have the bytes computed in the header?
                uint remainingBytesToRead = messageLength - CRDTConstants.MESSAGE_HEADER_LENGTH;

                if (remainingBytesToRead > memory.Length)
                    break;

                uint bodyLength = TypeLengthBytes(messageType, memory);

                if (messageType is not CRDTMessageType.PUT_COMPONENT_NETWORK || ComponentIdOfPutNetworkComponentType(memory) != NO_SYNC_COMPONENT_ID)
                {
                    output.Write(messageLength);
                    output.Write((uint)messageType);
                    totalWrite += 8;

                    memory.Slice(0, (int)bodyLength).CopyTo(output);
                    output = output.Slice((int)bodyLength);
                    totalWrite += (int)bodyLength;
                }

                memory = memory.Slice((int)bodyLength);
            }
        }

        /// <summary>
        /// Component header + variable (if appliable), input is after general header
        /// </summary>
        private static uint TypeLengthBytes(CRDTMessageType type, ReadOnlySpan<byte> memory) =>
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

        private static uint ComponentIdOfPutNetworkComponentType(ReadOnlySpan<byte> memory) =>
            memory.Slice(4).ReadConst<uint>(); // offset entityId
    }
}
