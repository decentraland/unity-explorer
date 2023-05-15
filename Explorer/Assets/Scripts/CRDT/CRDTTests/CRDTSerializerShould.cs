using CRDT.Memory;
using CRDT.Protocol;
using CRDT.Protocol.Factory;
using CRDT.Serializer;
using NUnit.Framework;
using System;
using System.Buffers;

namespace CRDT.CRDTTests
{
    [TestFixture]
    public class CRDTSerializerShould
    {
        private CRDTSerializer crdtSerializer;
        private CRDTPooledMemoryAllocator crdtPooledMemoryAllocator;

        [SetUp]
        public void SetUp()
        {
            crdtSerializer = new CRDTSerializer();
            crdtPooledMemoryAllocator = CRDTPooledMemoryAllocator.Create();
        }

        [Test]
        [TestCase(0, 1, 100, null,
            ExpectedResult = new byte[]
            {
                24, 0, 0, 0, //msg_length
                1, 0, 0, 0, //msg_type
                0, 0, 0, 0, //entityId
                1, 0, 0, 0, //componentId
                100, 0, 0, 0, //timestamp
                0, 0, 0, 0, //data_length
            })]
        [TestCase(32424, 67867, 2138996092, new byte[] { 42, 33, 67, 22 },
            ExpectedResult = new byte[]
            {
                28, 0, 0, 0, //msg_length
                1, 0, 0, 0, //msg_type
                168, 126, 0, 0, //entityId
                27, 9, 1, 0, //componentId
                124, 125, 126, 127, //timestamp
                4, 0, 0, 0, //data_length
                42, 33, 67, 22, //data
            })]
        public byte[] SerializeCorrectlyPutComponent(int entityId, int componentId, int timestamp, byte[] data)
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
            var destination = new byte[messageDataLength];
            var destinationSpan = destination.AsSpan();

            crdtSerializer.Serialize(ref destinationSpan, in processedMessage);

            // No extra bytes allocated
            Assert.AreEqual(0, destinationSpan.Length);

            return destination;
        }

        [Test]
        [TestCase(126, 44, 100,
            ExpectedResult = new byte[]
            {
                20, 0, 0, 0, //msg_length
                2, 0, 0, 0, //msg_type
                126, 0, 0, 0, //entityId
                44, 0, 0, 0, //componentId
                100, 0, 0, 0, //timestamp
            })]
        [TestCase(32424, 67867, 2138996092,
            ExpectedResult = new byte[]
            {
                20, 0, 0, 0, //msg_length
                2, 0, 0, 0, //msg_type
                168, 126, 0, 0, //entityId
                27, 9, 1, 0, //componentId
                124, 125, 126, 127, //timestamp
            })]
        public byte[] SerializeCorrectlyDeleteComponent(int entityId, int componentId, int timestamp)
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
            var destination = new byte[messageDataLength];
            var destinationSpan = destination.AsSpan();

            crdtSerializer.Serialize(ref destinationSpan, in processedMessage);

            // No extra bytes allocated
            Assert.AreEqual(0, destinationSpan.Length);

            return destination;
        }
    }
}
