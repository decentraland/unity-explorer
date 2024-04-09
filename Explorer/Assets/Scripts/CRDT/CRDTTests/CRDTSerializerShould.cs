using CRDT.Memory;
using CRDT.Protocol;
using CRDT.Protocol.Factory;
using CRDT.Serializer;
using NUnit.Framework;
using System;
using System.Buffers;
using System.Collections;
using System.Linq;
using Random = UnityEngine.Random;

namespace CRDT.CRDTTests
{

    public class CRDTSerializerShould
    {

        public void SetUp()
        {
            crdtSerializer = new CRDTSerializer();
            crdtPooledMemoryAllocator = CRDTPooledMemoryAllocator.Create();
        }

        private CRDTSerializer crdtSerializer;
        private CRDTPooledMemoryAllocator crdtPooledMemoryAllocator;

        private static IEnumerable SerializeCorrectlyPutComponentTestSource()
        {
            yield return new TestCaseData(0, 1, 100, null).Returns(new byte[]
            {
                24, 0, 0, 0, //msg_length
                1, 0, 0, 0, //msg_type
                0, 0, 0, 0, //entityId
                1, 0, 0, 0, //componentId
                100, 0, 0, 0, //timestamp
                0, 0, 0, 0, //data_length
            });

            yield return new TestCaseData(32424, 67867, 2138996092, new byte[] { 42, 33, 67, 22 }).Returns(new byte[]
            {
                28, 0, 0, 0, //msg_length
                1, 0, 0, 0, //msg_type
                168, 126, 0, 0, //entityId
                27, 9, 1, 0, //componentId
                124, 125, 126, 127, //timestamp
                4, 0, 0, 0, //data_length
                42, 33, 67, 22, //data
            });
        }


        public byte[] SerializeCorrectlyPutComponent(int entityId, int componentId, int timestamp, byte[] data) =>
            SerializeCorrectlyPutComponent(entityId, componentId, timestamp, data, false);


        public byte[] SerializeCorrectlyPutComponentWithContamination(int entityId, int componentId, int timestamp, byte[] data) =>
            SerializeCorrectlyPutComponent(entityId, componentId, timestamp, data, true);

        private byte[] SerializeCorrectlyPutComponent(int entityId, int componentId, int timestamp, byte[] data, bool contaminateBuffer)
        {
            ReadOnlyMemory<byte> dataMemory = data ?? ReadOnlyMemory<byte>.Empty;
            IMemoryOwner<byte> memoryOwner = crdtPooledMemoryAllocator.GetMemoryBuffer(dataMemory);
            var entity = new CRDTEntity(entityId);
            int messageDataLength = CRDTMessageSerializationUtils.GetMessageDataLength(CRDTMessageType.PUT_COMPONENT, in memoryOwner);

            var message = new CRDTMessage(
                CRDTMessageType.PUT_COMPONENT,
                entity,
                componentId,
                timestamp,
                memoryOwner
            );

            var processedMessage = new ProcessedCRDTMessage(message, messageDataLength);
            byte[] destination = contaminateBuffer ? GetContaminatedBuffer(messageDataLength) : new byte[messageDataLength];

            Span<byte> destinationSpan = destination.AsSpan(0, messageDataLength);

            crdtSerializer.Serialize(ref destinationSpan, in processedMessage);

            // No extra bytes allocated
            Assert.AreEqual(0, destinationSpan.Length);

            return destination.Take(messageDataLength).ToArray();
        }

        private static byte[] GetContaminatedBuffer(int dataLength)
        {
            var buffer = new byte[dataLength + Random.Range(1, dataLength * 2)];

            for (var i = 0; i < buffer.Length; i++)
                buffer[i] = (byte)Random.Range(1, 255);

            return buffer;
        }

        private static IEnumerable SerializeCorrectlyDeleteComponentTestSources()
        {
            yield return new TestCaseData(0, 1, 100).Returns(new byte[]
            {
                20, 0, 0, 0, //msg_length
                2, 0, 0, 0, //msg_type
                0, 0, 0, 0, //entityId
                1, 0, 0, 0, //componentId
                100, 0, 0, 0, //timestamp
            });

            yield return new TestCaseData(32424, 67867, 2138996092).Returns(new byte[]
            {
                20, 0, 0, 0, //msg_length
                2, 0, 0, 0, //msg_type
                168, 126, 0, 0, //entityId
                27, 9, 1, 0, //componentId
                124, 125, 126, 127, //timestamp
            });
        }


        public byte[] SerializeCorrectlyDeleteComponent(int entityId, int componentId, int timestamp) =>
            SerializeCorrectlyDeleteComponent(entityId, componentId, timestamp, false);


        public byte[] SerializeCorrectlyDeleteComponentWithContamination(int entityId, int componentId, int timestamp) =>
            SerializeCorrectlyDeleteComponent(entityId, componentId, timestamp, true);

        private byte[] SerializeCorrectlyDeleteComponent(int entityId, int componentId, int timestamp, bool contaminateBuffer)
        {
            var entity = new CRDTEntity(entityId);
            int messageDataLength = CRDTMessageSerializationUtils.GetMessageDataLength(CRDTMessageType.DELETE_COMPONENT, EmptyMemoryOwner<byte>.EMPTY);

            var message = new CRDTMessage(
                CRDTMessageType.DELETE_COMPONENT,
                entity,
                componentId,
                timestamp,
                EmptyMemoryOwner<byte>.EMPTY
            );

            var processedMessage = new ProcessedCRDTMessage(message, messageDataLength);
            byte[] destination = contaminateBuffer ? GetContaminatedBuffer(messageDataLength) : new byte[messageDataLength];
            Span<byte> destinationSpan = destination.AsSpan(0, messageDataLength);

            crdtSerializer.Serialize(ref destinationSpan, in processedMessage);

            // No extra bytes allocated
            Assert.AreEqual(0, destinationSpan.Length);

            return destination.Take(messageDataLength).ToArray();
        }
    }
}
