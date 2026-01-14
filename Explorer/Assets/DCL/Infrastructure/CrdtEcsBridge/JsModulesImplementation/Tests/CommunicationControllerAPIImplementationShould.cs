using CRDT.Protocol;
using CRDT;
using CrdtEcsBridge.JsModulesImplementation.Communications;
using CrdtEcsBridge.PoolsProviders;
using DCL.Ipfs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using ECS;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using SceneRuntime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Utility;

namespace CrdtEcsBridge.JsModulesImplementation.Tests
{
    public class CommunicationControllerAPIImplementationShould
    {
        private CommunicationsControllerAPIImplementation api;
        private TestSceneCommunicationPipe sceneCommunicationPipe;
        private IMessagePipe messagePipe;
        private IJsOperations jsOperations;
        private V8ScriptEngine engine;

        [SetUp]
        public void SetUp()
        {
            sceneCommunicationPipe = new TestSceneCommunicationPipe();

            ISceneData sceneData = Substitute.For<ISceneData>();
            sceneData.SceneEntityDefinition.Returns(new SceneEntityDefinition { id = "TEST_SCENE", metadata = new SceneMetadata { authoritativeMultiplayer = false } });

            ISceneStateProvider sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);

            IRealmData realmData = Substitute.For<IRealmData>();
            realmData.ScenesAreFixed.Returns(false);

            engine = new V8ScriptEngine();
            var arrayCtor = (ScriptObject)engine.Global.GetProperty("Array");
            var uint8ArrayCtor = (ScriptObject)engine.Global.GetProperty("Uint8Array");

            jsOperations = Substitute.For<IJsOperations>();
            jsOperations.NewArray().Returns(_ => arrayCtor.Invoke(true));

            jsOperations.GetTempUint8Array().Returns(_ => uint8ArrayCtor.Invoke(true, IJsOperations.LIVEKIT_MAX_SIZE));

            api = new CommunicationsControllerAPIImplementation(sceneData, sceneCommunicationPipe,
                jsOperations);
        }

        [Test]
        public void SendBinary([Range(0, 5)] int outerArraySize, [Range(1, 50)] int innerArrayMessagesCount)
        {
            // Generate random array of arrays

            var outerArray = new PoolableByteArray[outerArraySize];

            for (var i = 0; i < outerArraySize; i++)
            {
                byte[] messages = GetRandomMessagesSequence(innerArrayMessagesCount);
                outerArray[i] = new PoolableByteArray(messages, messages.Length, null);
            }

            api.SendBinary(outerArray);
            api.GetResult();

            var expectedCalls = outerArray.Select(o => o.Prepend((byte)ISceneCommunicationPipe.MsgType.Uint8Array).ToArray()).ToList();

            // Assert the 2d array is equal
            CollectionAssert.AreEqual(expectedCalls, sceneCommunicationPipe.sendMessageCalls);

            // Assert JSOperations called
            jsOperations.Received(1).NewArray();
        }

        [Test]
        public void OnMessageReceived()
        {
            const string WALLET_ID = "0x71C7656EC7ab88b098defB751B7401B5f6d8976F";

            byte[] data = GetRandomBytes(50).Prepend((byte)ISceneCommunicationPipe.MsgType.Uint8Array).ToArray();

            sceneCommunicationPipe.onSceneMessage.Invoke(new ISceneCommunicationPipe.DecodedMessage(data.AsSpan()[1..], WALLET_ID));

            byte[] walletBytes = Encoding.UTF8.GetBytes(WALLET_ID);

            IEnumerable<byte> expectedMessage =
                walletBytes.Concat(data.Skip(1))
                           .Prepend((byte)walletBytes.Length);

            // Check events to process
            Assert.AreEqual(1, api.EventsToProcess.Count);

            var eventBytes = new byte[walletBytes.Length + data.Length];
            api.EventsToProcess[0].ReadBytes(0ul, (ulong)eventBytes.Length, eventBytes, 0ul);

            CollectionAssert.AreEqual(expectedMessage, eventBytes);
        }

        [Test]
        public void ApplyFilterToCRDTMessages()
        {
            // Tests that CRDT messages (type 1) are filtered to remove NO_SYNC_COMPONENT_ID
            // while keeping valid messages before and after: VALID1 -> NO_SYNC (dropped) -> VALID2

            // CRDT message with three PUT_COMPONENT_NETWORK frames:
            //    - First uses a valid component id -> should be kept (VALID1)
            //    - Second uses NO_SYNC_COMPONENT_ID (2092194694) -> should be dropped by filter
            //    - Third uses a different valid component id -> should be kept (VALID2)
            const int PUT_NETWORK_MESSAGE_HEADER_LENGTH =
                CRDTConstants.MESSAGE_HEADER_LENGTH // message header
                + 20; // put network component header

            const byte COMMS_CRDT = 1;
            const uint NO_SYNC_COMPONENT_ID = 2092194694; // mirrors CRDTFilter's value

            int contentLength = 0; // No payload content for simplicity

            int crdtTotalLength = 1 // comms type
                                   + (PUT_NETWORK_MESSAGE_HEADER_LENGTH + contentLength) // VALID1
                                   + (PUT_NETWORK_MESSAGE_HEADER_LENGTH + contentLength) // NO_SYNC (dropped)
                                   + (PUT_NETWORK_MESSAGE_HEADER_LENGTH + contentLength); // VALID2

            var crdtMessage = new byte[crdtTotalLength];
            var crdtWrite = crdtMessage.AsSpan();
            crdtWrite[0] = COMMS_CRDT;
            var crdtBody = crdtWrite.Slice(1);

            // VALID1 frame (PUT_COMPONENT_NETWORK + valid component id)
            crdtBody.Write(PUT_NETWORK_MESSAGE_HEADER_LENGTH + contentLength);
            crdtBody.Write((uint)CRDTMessageType.PUT_COMPONENT_NETWORK);
            crdtBody.Write(111); // entity id
            crdtBody.Write(1000u); // component id -> should be kept
            crdtBody.Write(1); // timestamp
            crdtBody.Write(1); // network id
            crdtBody.Write(contentLength); // content length
            crdtBody = crdtBody.Slice(contentLength);

            // Droppable frame (PUT_COMPONENT_NETWORK + NO_SYNC component id)
            crdtBody.Write(PUT_NETWORK_MESSAGE_HEADER_LENGTH + contentLength);
            crdtBody.Write((uint)CRDTMessageType.PUT_COMPONENT_NETWORK);
            crdtBody.Write(222); // entity id
            crdtBody.Write(NO_SYNC_COMPONENT_ID); // component id -> this should trigger drop
            crdtBody.Write(2); // timestamp
            crdtBody.Write(2); // network id
            crdtBody.Write(contentLength); // content length
            crdtBody = crdtBody.Slice(contentLength);

            // VALID2 frame (PUT_COMPONENT_NETWORK + different valid component id)
            crdtBody.Write(PUT_NETWORK_MESSAGE_HEADER_LENGTH + contentLength);
            crdtBody.Write((uint)CRDTMessageType.PUT_COMPONENT_NETWORK);
            crdtBody.Write(333); // entity id
            crdtBody.Write(2000u); // component id -> should be kept
            crdtBody.Write(3); // timestamp
            crdtBody.Write(3); // network id
            crdtBody.Write(contentLength); // content length
            crdtBody = crdtBody.Slice(contentLength);

            var inputs = new PoolableByteArray[]
            {
                new PoolableByteArray(crdtMessage, crdtMessage.Length, null),
            };

            api.SendBinary(inputs);
            api.GetResult();

            // Expected: CRDT message should be filtered
            var filteredBuffer = new byte[crdtMessage.Length];
            CRDTFilter.FilterSceneMessageBatch(crdtMessage, filteredBuffer, out int filteredWrite);
            var expectedCrdtEncoded = new byte[filteredWrite + 1];
            expectedCrdtEncoded[0] = (byte)ISceneCommunicationPipe.MsgType.Uint8Array;
            Buffer.BlockCopy(filteredBuffer, 0, expectedCrdtEncoded, 1, filteredWrite);

            Assert.AreEqual(1, sceneCommunicationPipe.sendMessageCalls.Count, "Should have sent 1 message");

            // Verify CRDT message was filtered (should contain VALID1 and VALID2, but not NO_SYNC)
            CollectionAssert.AreEqual(expectedCrdtEncoded, sceneCommunicationPipe.sendMessageCalls[0], "CRDT message should be filtered");
            // Verify exactly one frame was removed: original had 3 frames, filtered should have 2
            int expectedFilteredSize = 1 + (2 * (PUT_NETWORK_MESSAGE_HEADER_LENGTH + contentLength)); // type byte + 2 valid frames
            Assert.AreEqual(expectedFilteredSize, filteredWrite, "Filtered CRDT should contain exactly 2 frames (VALID1 and VALID2)");
        }

        [Test]
        public void ApplyFilterToCRDTStateResponseMessages()
        {
            // Tests that RES_CRDT_STATE messages (type 3) are filtered to remove NO_SYNC_COMPONENT_ID
            // while preserving the address prefix and keeping valid messages: VALID1 -> NO_SYNC (dropped) -> VALID2

            // Build CRDT data with three PUT_COMPONENT_NETWORK frames:
            //    - First uses a valid component id -> should be kept (VALID1)
            //    - Second uses NO_SYNC_COMPONENT_ID (2092194694) -> should be dropped by filter
            //    - Third uses a different valid component id -> should be kept (VALID2)
            const int PUT_NETWORK_MESSAGE_HEADER_LENGTH =
                CRDTConstants.MESSAGE_HEADER_LENGTH // message header
                + 20; // put network component header

            const byte COMMS_RES_CRDT_STATE = 3;
            const uint NO_SYNC_COMPONENT_ID = 2092194694; // mirrors CRDTFilter's value

            int contentLength = 0; // No payload content for simplicity

            int crdtDataLength = (PUT_NETWORK_MESSAGE_HEADER_LENGTH + contentLength) // VALID1
                               + (PUT_NETWORK_MESSAGE_HEADER_LENGTH + contentLength) // NO_SYNC (dropped)
                               + (PUT_NETWORK_MESSAGE_HEADER_LENGTH + contentLength); // VALID2

            var crdtData = new byte[crdtDataLength];
            var crdtBody = crdtData.AsSpan();

            // VALID1 frame (PUT_COMPONENT_NETWORK + valid component id)
            crdtBody.Write(PUT_NETWORK_MESSAGE_HEADER_LENGTH + contentLength);
            crdtBody.Write((uint)CRDTMessageType.PUT_COMPONENT_NETWORK);
            crdtBody.Write(111); // entity id
            crdtBody.Write(1000u); // component id -> should be kept
            crdtBody.Write(1); // timestamp
            crdtBody.Write(1); // network id
            crdtBody.Write(contentLength); // content length
            crdtBody = crdtBody.Slice(contentLength);

            // Droppable frame (PUT_COMPONENT_NETWORK + NO_SYNC component id)
            crdtBody.Write(PUT_NETWORK_MESSAGE_HEADER_LENGTH + contentLength);
            crdtBody.Write((uint)CRDTMessageType.PUT_COMPONENT_NETWORK);
            crdtBody.Write(222); // entity id
            crdtBody.Write(NO_SYNC_COMPONENT_ID); // component id -> this should trigger drop
            crdtBody.Write(2); // timestamp
            crdtBody.Write(2); // network id
            crdtBody.Write(contentLength); // content length
            crdtBody = crdtBody.Slice(contentLength);

            // VALID2 frame (PUT_COMPONENT_NETWORK + different valid component id)
            crdtBody.Write(PUT_NETWORK_MESSAGE_HEADER_LENGTH + contentLength);
            crdtBody.Write((uint)CRDTMessageType.PUT_COMPONENT_NETWORK);
            crdtBody.Write(333); // entity id
            crdtBody.Write(2000u); // component id -> should be kept
            crdtBody.Write(3); // timestamp
            crdtBody.Write(3); // network id
            crdtBody.Write(contentLength); // content length
            crdtBody = crdtBody.Slice(contentLength);

            // RES_CRDT_STATE: Format is [type byte] + [1 byte: address length] + [address bytes] + [raw CRDT messages]
            string testAddress = "0x123";
            byte[] addressBytes = Encoding.UTF8.GetBytes(testAddress);
            byte addressLength = (byte)addressBytes.Length;

            var resMessage = new byte[1 + 1 + addressLength + crdtDataLength];
            var resSpan = resMessage.AsSpan();
            resSpan[0] = COMMS_RES_CRDT_STATE;
            resSpan[1] = addressLength;
            addressBytes.CopyTo(resSpan.Slice(2));
            crdtData.CopyTo(resSpan.Slice(2 + addressLength));

            var inputs = new PoolableByteArray[]
            {
                new PoolableByteArray(resMessage, resMessage.Length, null),
            };

            api.SendBinary(inputs);
            api.GetResult();

            // Expected: RES_CRDT_STATE should be filtered
            var filteredStateBuffer = new byte[resMessage.Length];
            CRDTFilter.FilterCRDTState(resMessage, filteredStateBuffer, out int filteredStateWrite);
            var expectedResEncoded = new byte[filteredStateWrite + 1];
            expectedResEncoded[0] = (byte)ISceneCommunicationPipe.MsgType.Uint8Array;
            Buffer.BlockCopy(filteredStateBuffer, 0, expectedResEncoded, 1, filteredStateWrite);

            Assert.AreEqual(1, sceneCommunicationPipe.sendMessageCalls.Count, "Should have sent 1 message");

            // Verify RES_CRDT_STATE was filtered and address preserved
            CollectionAssert.AreEqual(expectedResEncoded, sceneCommunicationPipe.sendMessageCalls[0], "RES_CRDT_STATE should be filtered");
            // Verify exactly one frame was removed from RES_CRDT_STATE: original had address + 3 frames, filtered should have address + 2 frames
            int expectedStateFilteredSize = 1 + 1 + addressLength + (2 * (PUT_NETWORK_MESSAGE_HEADER_LENGTH + contentLength)); // type + addr_len + addr + 2 valid frames
            Assert.AreEqual(expectedStateFilteredSize, filteredStateWrite, "Filtered RES_CRDT_STATE should contain exactly 2 frames (VALID1 and VALID2)");
            // Verify address is still present in the filtered output
            Assert.AreEqual(addressLength, filteredStateBuffer[1], "Address length should be preserved");
            var filteredAddressBytes = new byte[addressLength];
            Buffer.BlockCopy(filteredStateBuffer, 2, filteredAddressBytes, 0, addressLength);
            CollectionAssert.AreEqual(addressBytes, filteredAddressBytes, "Address should be preserved in filtered output");
        }

        private static byte[] GetRandomBytes(int size)
        {
            var rand = new Random();
            var buf = new byte[size];
            rand.NextBytes(buf);
            return buf;
        }

        private static byte[] GetRandomMessagesSequence(int count)
        {
            var rand = new Random();

            const int PUT_NETWORK_MESSAGE_HEADER_LENGTH =
                CRDTConstants.MESSAGE_HEADER_LENGTH // message header
                + 20; // put network component header

            int totalContentLength = 0;
            List<int> contentLengths = new List<int>();

            for (int i = 0; i < count; i++)
            {
                int length = rand.Next(0, 32) * 4;
                contentLengths.Add(length);
                totalContentLength += length;
            }

            var buf = new byte[1 + totalContentLength + (PUT_NETWORK_MESSAGE_HEADER_LENGTH * count)]; // 1 is for send type

            var writeHead = buf.AsSpan().Slice(1);

            foreach (int contentLength in contentLengths)
            {
                int messageLength = PUT_NETWORK_MESSAGE_HEADER_LENGTH + contentLength;
                writeHead.Write(messageLength);
                writeHead.Write((uint) CRDTMessageType.PUT_COMPONENT_NETWORK);
                writeHead.Write(rand.Next()); // entity id
                writeHead.Write(rand.Next(0, 2048)); // component id
                writeHead.Write(rand.Next()); // timestamp
                writeHead.Write(rand.Next()); // network id
                writeHead.Write(contentLength); // content length
                writeHead = writeHead.Slice(contentLength);
            }

            return buf;
        }

        [TearDown]
        public void TearDown()
        {
            api.Dispose();
            engine.Dispose();
        }

        // This class exists because we can't mock ReadOnlySpan (ref structs)
        private class TestSceneCommunicationPipe : ISceneCommunicationPipe
        {
            internal readonly List<byte[]> sendMessageCalls = new ();
            internal ISceneCommunicationPipe.SceneMessageHandler onSceneMessage;

            public void AddSceneMessageHandler(string sceneId, ISceneCommunicationPipe.MsgType msgType, ISceneCommunicationPipe.SceneMessageHandler onSceneMessage)
            {
                this.onSceneMessage = onSceneMessage;
            }

            public void RemoveSceneMessageHandler(string sceneId, ISceneCommunicationPipe.MsgType msgType, ISceneCommunicationPipe.SceneMessageHandler onSceneMessage) { }

            public void SendMessage(ReadOnlySpan<byte> message, string sceneId, ISceneCommunicationPipe.ConnectivityAssertiveness assertiveness, CancellationToken ct, string specialRecipient = null)
            {
                sendMessageCalls.Add(message.ToArray());
            }
        }
    }
}
