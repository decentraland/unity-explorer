using CRDT.Memory;
using CRDT.Serializer;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace CRDT.Protocol.Factory
{
    /// <summary>
    /// Encapsulation to contain logic of creating CRDT messages only
    /// in a sync with the protocol state
    /// </summary>
    internal static class CRDTMessagesFactory
    {
        public static ProcessedCRDTMessage CreateAppendMessage(this in CRDTProtocol.State state, CRDTEntity entity, int componentId, in IMemoryOwner<byte> data) =>
            CreateMessage(in state, CRDTMessageType.APPEND_COMPONENT, entity, componentId, data);

        public static ProcessedCRDTMessage CreatePutMessage(this in CRDTProtocol.State state, CRDTEntity entity, int componentId, in IMemoryOwner<byte> data) =>
            CreateMessage(in state, CRDTMessageType.PUT_COMPONENT, entity, componentId, data);

        public static ProcessedCRDTMessage CreateDeleteMessage(this in CRDTProtocol.State state, CRDTEntity entity, int componentId) =>
            CreateMessage(in state, CRDTMessageType.DELETE_COMPONENT, entity, componentId, EmptyMemoryOwner<byte>.EMPTY);

        /// <summary>
        /// Fills the array with messages corresponding to the current CRDT state
        /// Creates PUT, APPEND, DELETE COMPONENT and DELETE ENTITY messages
        /// </summary>
        /// <param name="state">The current CRDT state</param>
        /// <param name="preallocatedArray">An array big enough to fit all the messages</param>
        /// <returns>The number of bytes needed to serialize the messages</returns>
        public static int CreateMessagesFromTheCurrentState(this in CRDTProtocol.State state, ProcessedCRDTMessage[] preallocatedArray)
        {
            var index = 0;
            var numberOfBytesToSerialize = 0;

            foreach (var (componentId, componentStorage) in state.lwwComponents)
            {
                foreach (var (entity, entityComponentData) in componentStorage)
                {
                    var messageType = entityComponentData.isDeleted ? CRDTMessageType.DELETE_COMPONENT : CRDTMessageType.PUT_COMPONENT;

                    numberOfBytesToSerialize +=
                        AddCRDTMessage(preallocatedArray, entity, componentId, messageType, entityComponentData.Timestamp, entityComponentData.Data, ref index);
                }
            }

            // Data locality here is completely screwed but it is called from the separate thread so might be tolerated
            foreach (var (componentId, componentsStorage) in state.appendComponents)
            {
                foreach (var (entity, listOfEntityComponentData) in componentsStorage)
                {
                    foreach (var entityComponentData in listOfEntityComponentData)
                    {
                        numberOfBytesToSerialize +=
                            AddCRDTMessage(preallocatedArray, entity, componentId, CRDTMessageType.APPEND_COMPONENT, entityComponentData.Timestamp, entityComponentData.Data, ref index);
                    }
                }
            }

            foreach (var (number, version) in state.deletedEntities)
            {
                numberOfBytesToSerialize +=
                    AddCRDTMessage(preallocatedArray, CRDTEntity.Create(number, version), 0, CRDTMessageType.DELETE_ENTITY, 0, EmptyMemoryOwner<byte>.EMPTY, ref index);
            }

            return numberOfBytesToSerialize;
        }

        private static int AddCRDTMessage(ProcessedCRDTMessage[] preallocatedArray, CRDTEntity entity, int componentId,
            CRDTMessageType messageType,
            int timestamp,
            in IMemoryOwner<byte> data,
            ref int index)
        {
            var numberOfBytes = CRDTMessageSerializationUtils.GetMessageDataLength(messageType, in data);

            preallocatedArray[index] = new ProcessedCRDTMessage(new CRDTMessage(
                    messageType,
                    entity,
                    componentId,
                    timestamp,
                    data),
                numberOfBytes);

            index++;
            return numberOfBytes;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ProcessedCRDTMessage CreateMessage(in CRDTProtocol.State state, CRDTMessageType messageType, CRDTEntity entity, int componentId, in IMemoryOwner<byte> data)
        {
            var timestamp = 0;
            if (state.TryGetLWWComponentState(entity, componentId, out _, out _, out var storedData)) timestamp = storedData.Timestamp + 1;
            return new (new CRDTMessage(messageType, entity, componentId, timestamp, data), CRDTMessageSerializationUtils.GetMessageDataLength(messageType, in data));
        }
    }
}
