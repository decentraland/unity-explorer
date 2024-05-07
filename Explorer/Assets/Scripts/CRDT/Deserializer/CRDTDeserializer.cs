using CRDT.Memory;
using CRDT.Protocol;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Utility;

namespace CRDT.Deserializer
{
    public class CRDTDeserializer : ICRDTDeserializer
    {
        private readonly ICRDTMemoryAllocator crdtPooledMemoryAllocator;

        public CRDTDeserializer(ICRDTMemoryAllocator crdtPooledMemoryAllocator)
        {
            this.crdtPooledMemoryAllocator = crdtPooledMemoryAllocator;
        }

        void ICRDTDeserializer.DeserializeBatch(ref ReadOnlyMemory<byte> memory, IList<CRDTMessage> messages) =>
            DeserializeBatch(ref memory, messages);

        public void DeserializeBatch(ref ReadOnlyMemory<byte> memory, IList<CRDTMessage> messages)
        {
            // While we have a header to read
            while (memory.Length > CRDTConstants.MESSAGE_HEADER_LENGTH)
            {
                int messageLength = memory.Read<int>();
                CRDTMessageType messageType = memory.ReadEnumAs<CRDTMessageType, int>();

                // Message length lower than minimal, it's an invalid message
                if (messageLength <= CRDTConstants.MESSAGE_HEADER_LENGTH)
                    break;

                // Do we have the bytes computed in the header?
                int remainingBytesToRead = messageLength - CRDTConstants.MESSAGE_HEADER_LENGTH;

                if (remainingBytesToRead > memory.Length)
                    break;

                switch (messageType)
                {
                    case CRDTMessageType.PUT_COMPONENT:
                        if (TryDeserializePutComponent(ref memory, out CRDTMessage crdtMessage))
                            messages.Add(crdtMessage);

                        break;

                    case CRDTMessageType.DELETE_COMPONENT:
                        if (TryDeserializeDeleteComponent(ref memory, out crdtMessage))
                            messages.Add(crdtMessage);

                        break;

                    case CRDTMessageType.DELETE_ENTITY:
                        if (TryDeserializeDeleteEntity(ref memory, out crdtMessage))
                            messages.Add(crdtMessage);

                        break;

                    case CRDTMessageType.APPEND_COMPONENT:
                        if (TryDeserializeAppendComponent(ref memory, out crdtMessage))
                            messages.Add(crdtMessage);

                        break;

                    default:
                        memory = memory.Slice(remainingBytesToRead);
                        break;
                }
            }
        }

        public static bool TryDeserializeDeleteEntity(ref ReadOnlyMemory<byte> memory, out CRDTMessage crdtMessage)
        {
            var shift = 0;
            ReadOnlySpan<byte> memorySpan = memory.Span;
            crdtMessage = default(CRDTMessage);

            if (memorySpan.Length < CRDTConstants.CRDT_DELETE_ENTITY_HEADER_LENGTH)
                return false;

            CRDTEntity entityId = memorySpan.Read<CRDTEntity>(ref shift);
            memory = memory.Slice(shift);

            crdtMessage = new CRDTMessage(CRDTMessageType.DELETE_ENTITY, entityId, 0, 0, EmptyMemoryOwner<byte>.EMPTY);
            return true;
        }

        public static bool TryDeserializeDeleteComponent(ref ReadOnlyMemory<byte> memory, out CRDTMessage crdtMessage)
        {
            var shift = 0;
            ReadOnlySpan<byte> memorySpan = memory.Span;
            crdtMessage = default(CRDTMessage);

            if (memorySpan.Length < CRDTConstants.CRDT_DELETE_COMPONENT_HEADER_LENGTH)
                return false;

            CRDTEntity entityId = memorySpan.Read<CRDTEntity>(ref shift);
            int componentId = memorySpan.Read<int>(ref shift);
            int timestamp = memorySpan.Read<int>(ref shift);

            memory = memory.Slice(shift);

            crdtMessage = new CRDTMessage(CRDTMessageType.DELETE_COMPONENT, entityId, componentId, timestamp, EmptyMemoryOwner<byte>.EMPTY);
            return true;
        }

        public bool TryDeserializeAppendComponent(ref ReadOnlyMemory<byte> memory, out CRDTMessage crdtMessage)
        {
            var shift = 0;
            ReadOnlySpan<byte> memorySpan = memory.Span;
            crdtMessage = default(CRDTMessage);

            if (memorySpan.Length < CRDTConstants.CRDT_APPEND_COMPONENT_HEADER_LENGTH)
                return false;

            DeserializerParameters(ref memorySpan, out CRDTEntity entityId, out int componentId, out int timestamp, out int dataLength, ref shift);

            if (TryReturnInvalidDataLength(ref memory, dataLength, memorySpan, shift))
                return false;

            // Slice from memory
            IMemoryOwner<byte> memoryOwner = crdtPooledMemoryAllocator.GetMemoryBuffer(memory, shift, dataLength);
            memory = memory.Slice(shift + dataLength);

            crdtMessage = new CRDTMessage(CRDTMessageType.APPEND_COMPONENT, entityId, componentId, timestamp, memoryOwner);
            return true;
        }

        public bool TryDeserializePutComponent(ref ReadOnlyMemory<byte> memory, out CRDTMessage crdtMessage)
        {
            var shift = 0;
            ReadOnlySpan<byte> memorySpan = memory.Span;
            crdtMessage = default(CRDTMessage);

            if (memorySpan.Length < CRDTConstants.CRDT_PUT_COMPONENT_HEADER_LENGTH)
                return false;

            DeserializerParameters(ref memorySpan, out CRDTEntity entityId, out int componentId, out int timestamp, out int dataLength, ref shift);

            if (TryReturnInvalidDataLength(ref memory, dataLength, memorySpan, shift))
                return false;

            IMemoryOwner<byte> memoryOwner = crdtPooledMemoryAllocator.GetMemoryBuffer(memory, shift, dataLength);

            //Forwarding the memory to the next message
            memory = memory.Slice(shift + dataLength);

            crdtMessage = new CRDTMessage(CRDTMessageType.PUT_COMPONENT, entityId, componentId, timestamp, memoryOwner);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryReturnInvalidDataLength(ref ReadOnlyMemory<byte> memory, int dataLength,
            ReadOnlySpan<byte> memorySpan, int shift)
        {
            if (dataLength < 0)
                throw new Exception($"Number is negative in the data length: {dataLength}");

            if (dataLength > memorySpan.Length)
            {
                memory = memory.Slice(shift);
                return true;
            }

            return false;
        }

        private static void DeserializerParameters(ref ReadOnlySpan<byte> memorySpan, out CRDTEntity entityId, out int componentId, out int timestamp, out int dataLength,
            ref int shift)
        {
            entityId = memorySpan.Read<CRDTEntity>(ref shift);
            componentId = memorySpan.Read<int>(ref shift);
            timestamp = memorySpan.Read<int>(ref shift);
            dataLength = memorySpan.Read<int>(ref shift);
        }
    }
}
