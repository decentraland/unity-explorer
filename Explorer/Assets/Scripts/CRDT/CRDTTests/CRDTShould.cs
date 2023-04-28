using CRDT.Deserializer;
using CRDT.Protocol;
using CRDT.Protocol.Factory;
using CRDT.Serializer;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CRDT.CRDTTests
{
    [TestFixture]
    public class CRDTShould
    {
        private ICRDTSerializer serializer;
        private ICRDTDeserializer deserializer;

        [SetUp]
        public void SetUp()
        {
            serializer = new CRDTSerializer();
            deserializer = new CRDTDeserializer();
        }

        [Test]
        [TestCaseSource(nameof(GetIndividualMessages))]
        public void HaveDeserializerAndSerializerOnParWithIndividualMessages(CRDTMessage expected)
        {
            var messageDataLength = expected.GetMessageDataLength();
            var processedMessage = new ProcessedCRDTMessage(expected, messageDataLength);

            var destination = new byte[messageDataLength];
            var destinationSpan = destination.AsSpan();
            serializer.Serialize(ref destinationSpan, in processedMessage);

            var memory = new ReadOnlyMemory<byte>(destination);
            var messages = new List<CRDTMessage>();

            deserializer.DeserializeBatch(ref memory, messages);

            CollectionAssert.AreEqual(new[] { expected }, messages);
        }

        [Test]
        [TestCaseSource(nameof(GetBatches))]
        public void HaveDeserializerAndSerializerOnParWithBatches(CRDTMessage[] expected)
        {
            var dataLength = expected.Aggregate(0, (acc, msg) => acc + msg.GetMessageDataLength());
            var destination = new byte[dataLength];
            var destinationSpan = destination.AsSpan();

            foreach (CRDTMessage message in expected)
            {
                var messageDataLength = message.GetMessageDataLength();
                var processedMessage = new ProcessedCRDTMessage(message, messageDataLength);
                serializer.Serialize(ref destinationSpan, in processedMessage);
            }

            var memory = new ReadOnlyMemory<byte>(destination);
            var messages = new List<CRDTMessage>();

            deserializer.DeserializeBatch(ref memory, messages);

            CollectionAssert.AreEqual(expected, messages);
        }

        private static object[] GetIndividualMessages()
        {
            return new object[]
            {
                new CRDTMessage(CRDTMessageType.APPEND_COMPONENT, 125, 89233, 922, Encoding.UTF8.GetBytes("TEST_BYTES_1")),
                new CRDTMessage(CRDTMessageType.PUT_COMPONENT, 233, 4534232, 222, Encoding.UTF8.GetBytes("TEST_BYTES_2")),
                new CRDTMessage(CRDTMessageType.DELETE_COMPONENT, 210, 32332, 1, ReadOnlyMemory<byte>.Empty),
                new CRDTMessage(CRDTMessageType.DELETE_ENTITY, 123, 0, 0, ReadOnlyMemory<byte>.Empty),
            };
        }

        private static object[] GetBatches()
        {
            return new object[]
            {
                new CRDTMessage[]
                {
                    new (CRDTMessageType.APPEND_COMPONENT, 100, 100, 232, Encoding.UTF8.GetBytes("TEST_BYTES_1")),
                    new (CRDTMessageType.PUT_COMPONENT, 4343, 121303, 24343335, Encoding.UTF8.GetBytes("TEST_BYTES_4")),
                },
                new CRDTMessage[]
                {
                    new (CRDTMessageType.APPEND_COMPONENT, 100, 100, 232, Encoding.UTF8.GetBytes("TEST_BYTES_1")),
                    new (CRDTMessageType.PUT_COMPONENT, 4343, 121303, 24343335, Encoding.UTF8.GetBytes("TEST_BYTES_4")),
                    new (CRDTMessageType.DELETE_COMPONENT, 210, 32332, 1, ReadOnlyMemory<byte>.Empty),
                    new (CRDTMessageType.DELETE_ENTITY, 123, 0, 0, ReadOnlyMemory<byte>.Empty),
                },
                new CRDTMessage[]
                {
                    new (CRDTMessageType.DELETE_COMPONENT, 210, 32332, 1, ReadOnlyMemory<byte>.Empty),
                    new (CRDTMessageType.DELETE_COMPONENT, 42435, 444, int.MinValue, ReadOnlyMemory<byte>.Empty),
                    new (CRDTMessageType.DELETE_COMPONENT, 3434, 12112, 1, ReadOnlyMemory<byte>.Empty),
                    new (CRDTMessageType.DELETE_COMPONENT, 343, 48989, 332, ReadOnlyMemory<byte>.Empty),
                    new (CRDTMessageType.DELETE_COMPONENT, 962, 33556, 1, ReadOnlyMemory<byte>.Empty),
                    new (CRDTMessageType.DELETE_COMPONENT, 1456, 999, int.MaxValue, ReadOnlyMemory<byte>.Empty),
                    new (CRDTMessageType.DELETE_ENTITY, 654, 0, 0, ReadOnlyMemory<byte>.Empty),
                    new (CRDTMessageType.DELETE_ENTITY, 6654, 0, 0, ReadOnlyMemory<byte>.Empty),
                    //new (CRDTMessageType.DELETE_ENTITY, 66712, 0, 0, ReadOnlyMemory<byte>.Empty)
                }
            };
        }
    }
}
