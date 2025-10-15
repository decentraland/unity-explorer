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
            sceneData.SceneEntityDefinition.Returns(new SceneEntityDefinition { id = "TEST_SCENE" });

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
        public void ApplyFilterOnlyToCRDTMessages()
        {
            // Build three messages: CRDT (should be filtered), REQ_CRDT_STATE and RES_CRDT_STATE (should not be filtered)

            // 1) CRDT message with two PUT_COMPONENT_NETWORK frames:
            //    - First uses NO_SYNC_COMPONENT_ID (2092194694) -> should be dropped by filter
            //    - Second uses a different component id -> should be kept
            const int PUT_NETWORK_MESSAGE_HEADER_LENGTH =
                CRDTConstants.MESSAGE_HEADER_LENGTH // message header
                + 20; // put network component header

            const byte COMMS_CRDT = 1;
            const byte COMMS_REQ_CRDT_STATE = 2;
            const byte COMMS_RES_CRDT_STATE = 3;

            const uint NO_SYNC_COMPONENT_ID = 2092194694; // mirrors CRDTFilter's value

            int droppableContentLength = 0;
            int keepableContentLength = 0;

            int crdtTotalLength = 1 // comms type
                                   + (PUT_NETWORK_MESSAGE_HEADER_LENGTH + droppableContentLength)
                                   + (PUT_NETWORK_MESSAGE_HEADER_LENGTH + keepableContentLength);

            var crdtMessage = new byte[crdtTotalLength];
            var crdtWrite = crdtMessage.AsSpan();
            crdtWrite[0] = COMMS_CRDT;
            var crdtBody = crdtWrite.Slice(1);

            // Droppable frame (PUT_COMPONENT_NETWORK + NO_SYNC component id)
            crdtBody.Write(PUT_NETWORK_MESSAGE_HEADER_LENGTH + droppableContentLength);
            crdtBody.Write((uint)CRDTMessageType.PUT_COMPONENT_NETWORK);
            crdtBody.Write(123); // entity id
            crdtBody.Write(NO_SYNC_COMPONENT_ID); // component id -> this should trigger drop
            crdtBody.Write(1); // timestamp
            crdtBody.Write(1); // network id
            crdtBody.Write(droppableContentLength); // content length
            crdtBody = crdtBody.Slice(droppableContentLength);

            // Keepable frame (PUT_COMPONENT_NETWORK + different component id)
            crdtBody.Write(PUT_NETWORK_MESSAGE_HEADER_LENGTH + keepableContentLength);
            crdtBody.Write((uint)CRDTMessageType.PUT_COMPONENT_NETWORK);
            crdtBody.Write(456); // entity id
            crdtBody.Write(1234u); // component id -> should be kept
            crdtBody.Write(2); // timestamp
            crdtBody.Write(2); // network id
            crdtBody.Write(keepableContentLength); // content length
            crdtBody = crdtBody.Slice(keepableContentLength);

            // 2) REQ_CRDT_STATE: arbitrary payload
            var reqMessage = new byte[] { COMMS_REQ_CRDT_STATE, 9, 8, 7, 6 };

            // 3) RES_CRDT_STATE: arbitrary payload
            var resMessage = new byte[] { COMMS_RES_CRDT_STATE, 1, 2, 3 };

            var inputs = new PoolableByteArray[]
            {
                new PoolableByteArray(crdtMessage, crdtMessage.Length, null),
                new PoolableByteArray(reqMessage, reqMessage.Length, null),
                new PoolableByteArray(resMessage, resMessage.Length, null),
            };

            api.SendBinary(inputs);
            api.GetResult();

            // Expected 1: CRDT message should be filtered
            var filteredBuffer = new byte[crdtMessage.Length];
            CRDTFilter.FilterSceneMessageBatch(crdtMessage, filteredBuffer, out int filteredWrite);
            var expectedCrdtEncoded = new byte[filteredWrite + 1];
            expectedCrdtEncoded[0] = (byte)ISceneCommunicationPipe.MsgType.Uint8Array;
            Buffer.BlockCopy(filteredBuffer, 0, expectedCrdtEncoded, 1, filteredWrite);

            // Expected 2: REQ/RES should be forwarded unchanged (apart from the transport type prefix)
            var expectedReqEncoded = reqMessage.Prepend((byte)ISceneCommunicationPipe.MsgType.Uint8Array).ToArray();
            var expectedResEncoded = resMessage.Prepend((byte)ISceneCommunicationPipe.MsgType.Uint8Array).ToArray();

            Assert.AreEqual(3, sceneCommunicationPipe.sendMessageCalls.Count);
            CollectionAssert.AreEqual(expectedCrdtEncoded, sceneCommunicationPipe.sendMessageCalls[0]);
            CollectionAssert.AreEqual(expectedReqEncoded, sceneCommunicationPipe.sendMessageCalls[1]);
            CollectionAssert.AreEqual(expectedResEncoded, sceneCommunicationPipe.sendMessageCalls[2]);
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
