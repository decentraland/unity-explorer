using CRDT.Deserializer;
using CRDT.Memory;
using CRDT.Protocol;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CRDT.CRDTTests
{

    public class CRDTDeserializerShould
    {

        public void SetUp()
        {
            crdtPooledMemoryAllocator = CRDTPooledMemoryAllocator.Create();
            deserializer = new CRDTDeserializer(crdtPooledMemoryAllocator);
        }

        private static readonly byte[] componentDataBytes =
        {
            64, 73, 15, 219, 64, 73, 15, 219,
            64, 73, 15, 219, 64, 73, 15, 219, 64, 73, 15, 219,
            64, 73, 15, 219, 64, 73, 15, 219, 64, 73, 15, 219,
            64, 73, 15, 219, 64, 73, 15, 219,
        };

        private CRDTPooledMemoryAllocator crdtPooledMemoryAllocator;
        private CRDTDeserializer deserializer;


        public void ParsePutComponent()
        {
            byte[] bytes =
            {
                64, 0, 0, 0,
                1, 0, 0, 0,
                154, 2, 0, 0,
                1, 0, 0, 0,
                242, 29, 0, 0,
                40, 0, 0, 0,
            };

            bytes = bytes.Concat(componentDataBytes).ToArray();

            var expectedMessage = new CRDTMessage(
                CRDTMessageType.PUT_COMPONENT,
                new CRDTEntity(666),
                1,
                7666,
                crdtPooledMemoryAllocator.GetMemoryBuffer(componentDataBytes)
            );

            TestInput(bytes, new[] { expectedMessage });
        }


        public void ParseAppendComponent()
        {
            byte[] bytes =
            {
                64, 0, 0, 0,
                4, 0, 0, 0,
                154, 2, 0, 0,
                1, 0, 0, 0,
                242, 29, 0, 0,
                40, 0, 0, 0,
            };

            bytes = bytes.Concat(componentDataBytes).ToArray();

            var expectedMessage = new CRDTMessage(
                CRDTMessageType.APPEND_COMPONENT,
                new CRDTEntity(666),
                1,
                7666,
                crdtPooledMemoryAllocator.GetMemoryBuffer(componentDataBytes)
            );

            TestInput(bytes, new[] { expectedMessage });
        }


        public void ParseDeleteEntity()
        {
            byte[] bytes =
            {
                12, 0, 0, 0,
                3, 0, 0, 0,
                154, 2, 0, 0,
            };

            TestInput(bytes, new[] { new CRDTMessage(CRDTMessageType.DELETE_ENTITY, new CRDTEntity(666), 0, 0, EmptyMemoryOwner<byte>.EMPTY) });
        }


        public void ParseDeleteComponent()
        {
            byte[] bytes =
            {
                20, 0, 0, 0,
                2, 0, 0, 0,
                154, 2, 0, 0,
                100, 0, 0, 0,
                100, 0, 0, 0,
            };

            TestInput(bytes, new[] { new CRDTMessage(CRDTMessageType.DELETE_COMPONENT, new CRDTEntity(666), 100, 100, EmptyMemoryOwner<byte>.EMPTY) });
        }


        public void ParseTwoMessagesInSameByteArray()
        {
            byte[] bytesMsgA =
            {
                64, 0, 0, 0, 1, 0, 0, 0, 154, 2, 0, 0,
                1, 0, 0, 0, 242, 29, 0, 0,
                40, 0, 0, 0,
            };

            bytesMsgA = bytesMsgA.Concat(componentDataBytes).ToArray();

            byte[] bytesMsgB =
            {
                64, 0, 0, 0, 1, 0, 0, 0, 154, 2, 0, 0,
                1, 0, 0, 0, 242, 29, 0, 0,
                40, 0, 0, 0,
            };

            bytesMsgB = bytesMsgB.Concat(componentDataBytes).ToArray();

            byte[] bytes = bytesMsgA.Concat(bytesMsgB).ToArray();

            var expectedComponentHeader = new CRDTMessage(CRDTMessageType.PUT_COMPONENT, new CRDTEntity(666), 1, 7666, crdtPooledMemoryAllocator.GetMemoryBuffer(componentDataBytes));

            TestInput(bytes, new[] { expectedComponentHeader, expectedComponentHeader });
        }


        public void CopyByteArrayDataCorrectly()
        {
            byte[] binaryMessage =
            {
                0, 0, 0, 64, 0, 0, 0, 1, 0, 0, 2, 154,
                0, 0, 0, 1, 0, 0, 29, 242,
                0, 0, 0, 40, 64, 73, 15, 219, 64, 73, 15, 219,
                64, 73, 15, 219, 64, 73, 15, 219, 64, 73, 15, 219,
                64, 73, 15, 219, 64, 73, 15, 219, 64, 73, 15, 219,
                64, 73, 15, 219, 64, 73, 15, 219,
            };

            int dataStart = 8 + 16; //sizeof(CRDTMessageHeader) + sizeof(CRDTComponentMessageHeader)
            var data = new byte[binaryMessage.Length - dataStart];
            Buffer.BlockCopy(binaryMessage, dataStart, data, 0, data.Length);

            var list = new List<CRDTMessage>();
            ReadOnlyMemory<byte> binaryMessageMemory = binaryMessage;
            deserializer.DeserializeBatch(ref binaryMessageMemory, list);

            foreach (CRDTMessage crdtMessage in list)
                Assert.IsTrue(data.AsSpan().SequenceEqual(crdtMessage.Data.Memory.Span), "Messages are not equal");
        }


        public void SetDataOnPutComponentWithEmptyPayload()
        {
            byte[] message =
            {
                24, 0, 0, 0, //header: length = 24
                1, 0, 0, 0, //header: type = 1 (PUT_COMPONENT)
                127, 0, 0, 0, // component: entityId
                1, 0, 0, 0, // component: componentId
                1, 0, 0, 0, // component: timestamp (int32)
                0, 0, 0, 0, // component: data-length (0)
            };

            ReadOnlyMemory<byte> memory = message;

            var list = new List<CRDTMessage>();

            var expected = new CRDTMessage[] { new (CRDTMessageType.PUT_COMPONENT, new CRDTEntity(127), 1, 1, EmptyMemoryOwner<byte>.EMPTY) };
            deserializer.DeserializeBatch(ref memory, list);

            CollectionAssert.AreEqual(expected, list);
        }


        public void SkipUnknownMessageType()
        {
            byte[] message =
            {
                0, 0, 0, 27, //header: length = 27
                0, 0, 0, 44, //header: type = 44 (unknown)
                0, 0, 0, 0, 0, 0, 0, //b: 11
                0, 0, 0, 0, 0, 0, 18, 139, //b:8
            };

            var list = new List<CRDTMessage>();
            ReadOnlyMemory<byte> memory = message;
            deserializer.DeserializeBatch(ref memory, list);

            CollectionAssert.IsEmpty(list);
        }


        public void DeserializeMixBatch()
        {
            byte[] append =
            {
                64, 0, 0, 0,
                4, 0, 0, 0,
                154, 2, 0, 0,
                1, 0, 0, 0,
                242, 29, 0, 0,
                40, 0, 0, 0,
            };

            byte[] put =
            {
                64, 0, 0, 0,
                1, 0, 0, 0,
                154, 2, 0, 0,
                1, 0, 0, 0,
                242, 29, 0, 0,
                40, 0, 0, 0,
            };

            byte[] deleteComponent =
            {
                20, 0, 0, 0,
                2, 0, 0, 0,
                154, 2, 0, 0,
                100, 0, 0, 0,
                100, 0, 0, 0,
            };

            byte[] deleteEntity =
            {
                12, 0, 0, 0,
                3, 0, 0, 0,
                154, 2, 0, 0,
            };

            byte[] all = append.Concat(componentDataBytes).Concat(put).Concat(componentDataBytes).Concat(deleteComponent).Concat(deleteEntity).ToArray();

            TestInput(all, new[]
            {
                new CRDTMessage(
                    CRDTMessageType.APPEND_COMPONENT,
                    new CRDTEntity(666),
                    1,
                    7666,
                    crdtPooledMemoryAllocator.GetMemoryBuffer(componentDataBytes)
                ),
                new CRDTMessage(
                    CRDTMessageType.PUT_COMPONENT,
                    new CRDTEntity(666),
                    1,
                    7666,
                    crdtPooledMemoryAllocator.GetMemoryBuffer(componentDataBytes)
                ),
                new CRDTMessage(
                    CRDTMessageType.DELETE_COMPONENT,
                    new CRDTEntity(666),
                    100,
                    100,
                    EmptyMemoryOwner<byte>.EMPTY
                ),
                new CRDTMessage(
                    CRDTMessageType.DELETE_ENTITY,
                    new CRDTEntity(666),
                    0,
                    0,
                    EmptyMemoryOwner<byte>.EMPTY
                ),
            });
        }

        private void TestInput(ReadOnlyMemory<byte> memory, CRDTMessage[] crdtMessages)
        {
            var list = new List<CRDTMessage>();
            deserializer.DeserializeBatch(ref memory, list);

            CollectionAssert.AreEqual(crdtMessages, list);
        }
    }
}
