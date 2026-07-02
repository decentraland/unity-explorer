using CrdtEcsBridge.JsModulesImplementation.Communications;
using DCL.Ipfs;
using DCL.Multiplayer.Connections.RoomHubs;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using SceneRunner.Scene.ExceptionsHandling;
using SceneRuntime;
using SceneRuntime.Apis.Modules.CommsApi;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace SceneRuntime.Tests
{
    public class CommsApiWrapShould
    {
        private const string TEST_SCENE_ID = "test-scene-123";

        private CommsApiWrap commsApi;
        private TestSceneCommunicationPipe pipe;
        private IRoomHub roomHub;
        private ISceneExceptionsHandler exceptionsHandler;
        private CancellationTokenSource cts;

        [SetUp]
        public void SetUp()
        {
            pipe = new TestSceneCommunicationPipe();

            ISceneData sceneData = Substitute.For<ISceneData>();
            sceneData.SceneEntityDefinition.Returns(new SceneEntityDefinition { id = TEST_SCENE_ID, metadata = new SceneMetadata() });

            roomHub = Substitute.For<IRoomHub>();
            exceptionsHandler = Substitute.For<ISceneExceptionsHandler>();
            cts = new CancellationTokenSource();

            commsApi = new CommsApiWrap(roomHub, pipe, sceneData, exceptionsHandler, cts);
        }

        [TearDown]
        public void TearDown()
        {
            commsApi.Dispose();
            cts.Dispose();
        }

        [Test]
        public void RegisterHandlerOnConstruction()
        {
            //Assert
            Assert.AreEqual(TEST_SCENE_ID, pipe.registeredSceneId);
            Assert.AreEqual(ISceneCommunicationPipe.MsgType.CommsData, pipe.registeredMsgType);
            Assert.IsNotNull(pipe.onSceneMessage);
        }

        [Test]
        public void UnregisterHandlerOnDispose()
        {
            //Act
            commsApi.Dispose();

            //Assert
            Assert.IsTrue(pipe.handlerRemoved);
        }

        [Test]
        public void PublishDataWithMsgTypeCommsDataPrefix()
        {
            //Arrange
            string topic = "my-topic";
            string data = "{\"type\":\"test\"}";
            byte[] topicBytes = Encoding.UTF8.GetBytes(topic);

            //Act
            commsApi.PublishData(topic, data);

            //Assert
            Assert.AreEqual(1, pipe.sendMessageCalls.Count);

            byte[] sent = pipe.sendMessageCalls[0];
            Assert.AreEqual((byte)ISceneCommunicationPipe.MsgType.CommsData, sent[0], "First byte should be MsgType.CommsData.");

            // Wire format after MsgType: [topicLen 2 bytes LE][topic UTF-8][data UTF-8]
            ushort topicLen = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(sent.AsSpan(1));
            Assert.AreEqual(topicBytes.Length, topicLen, "Topic length header should match.");

            string decodedTopic = Encoding.UTF8.GetString(sent, 3, topicLen);
            Assert.AreEqual(topic, decodedTopic, "Topic should match.");

            string decodedData = Encoding.UTF8.GetString(sent, 3 + topicLen, sent.Length - 3 - topicLen);
            Assert.AreEqual(data, decodedData, "Data payload should match.");
        }

        [Test]
        public void PublishAndReceiveRoundTrip()
        {
            //Arrange
            string topic = "round-trip";
            string data = "{\"type\":\"hello\"}";
            string senderIdentity = "0xABCD";

            commsApi.SubscribeToTopic(topic);

            //Act — publish to capture wire bytes.
            commsApi.PublishData(topic, data);

            Assert.AreEqual(1, pipe.sendMessageCalls.Count);
            byte[] wireBytes = pipe.sendMessageCalls[0];

            // Simulate receive: SceneCommunicationPipe.DecodeMessage strips byte[0] (MsgType).
            ReadOnlySpan<byte> afterMsgType = wireBytes.AsSpan(1);
            pipe.onSceneMessage.Invoke(new ISceneCommunicationPipe.DecodedMessage(afterMsgType, senderIdentity, isTrustedSource: true));

            //Assert — ConsumeMessages returns JSON; inner data string is JSON-escaped by JsonTextWriter.
            string json = commsApi.ConsumeMessages(topic);
            Assert.That(json, Does.Contain("\"sender\":\"0xABCD\""));
            Assert.That(json, Does.Contain("\"data\":"));
            Assert.That(json, Does.Contain("hello"));
        }

        [Test]
        public void ReturnEmptyArrayForUnsubscribedTopic()
        {
            //Act
            string result = commsApi.ConsumeMessages("unknown-topic");

            //Assert
            Assert.AreEqual("[]", result);
        }

        [Test]
        public void ReturnEmptyArrayWhenNoMessages()
        {
            //Arrange
            commsApi.SubscribeToTopic("empty-topic");

            //Act
            string result = commsApi.ConsumeMessages("empty-topic");

            //Assert
            Assert.AreEqual("[]", result);
        }

        [Test]
        public void DrainBufferOnConsume()
        {
            //Arrange
            commsApi.SubscribeToTopic("drain-test");
            SimulateIncomingMessage("drain-test", "{\"v\":1}", "sender1");
            SimulateIncomingMessage("drain-test", "{\"v\":2}", "sender2");

            //Act
            string first = commsApi.ConsumeMessages("drain-test");
            string second = commsApi.ConsumeMessages("drain-test");

            //Assert — ConsumeMessages JSON-escapes inner data, so check sender identities.
            Assert.That(first, Does.Contain("sender1"));
            Assert.That(first, Does.Contain("sender2"));
            Assert.AreEqual("[]", second, "Buffer should be drained after first consume.");
        }

        [Test]
        public void DropMessagesForUnsubscribedTopics()
        {
            //Arrange — do NOT subscribe to "ignored-topic".
            SimulateIncomingMessage("ignored-topic", "{}", "sender");

            //Act
            commsApi.SubscribeToTopic("ignored-topic");
            string result = commsApi.ConsumeMessages("ignored-topic");

            //Assert
            Assert.AreEqual("[]", result, "Messages before subscription should be dropped.");
        }

        [Test]
        public void RejectNullData()
        {
            //Act
            commsApi.PublishData("topic", null);

            //Assert
            Assert.AreEqual(0, pipe.sendMessageCalls.Count);
        }

        [Test]
        public void RejectEmptyData()
        {
            //Act
            commsApi.PublishData("topic", "");

            //Assert
            Assert.AreEqual(0, pipe.sendMessageCalls.Count);
        }

        [Test]
        public void EnforceRateLimit()
        {
            //Arrange
            string data = "{\"x\":1}";

            //Act — send MAX_MESSAGES_PER_SECOND + 1 messages.
            for (int i = 0; i <= 10; i++)
                commsApi.PublishData("rate-topic", data);

            //Assert — 11th message should be dropped.
            Assert.AreEqual(10, pipe.sendMessageCalls.Count, "Should cap at MAX_MESSAGES_PER_SECOND.");
        }

        [Test]
        public void IsolateRateLimitPerTopic()
        {
            //Arrange
            string data = "{\"x\":1}";

            //Act
            for (int i = 0; i < 10; i++)
                commsApi.PublishData("topic-a", data);

            commsApi.PublishData("topic-b", data);

            //Assert — topic-a exhausted, topic-b should still work.
            Assert.AreEqual(11, pipe.sendMessageCalls.Count);
        }

        [Test]
        public void RejectOversizedData()
        {
            //Arrange — data larger than LIVEKIT_MAX_SIZE (13 312 bytes).
            string topic = "big-topic";
            string data = new string('A', IJsOperations.LIVEKIT_MAX_SIZE);

            //Act
            commsApi.PublishData(topic, data);

            //Assert
            Assert.AreEqual(0, pipe.sendMessageCalls.Count, "Oversized payload should be silently dropped.");
        }

        private void SimulateIncomingMessage(string topic, string data, string senderIdentity)
        {
            // Encode in wire format expected by OnDataReceived: [topicLen 2 bytes LE][topic UTF-8][data UTF-8]
            byte[] topicBytes = Encoding.UTF8.GetBytes(topic);
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);
            byte[] encoded = new byte[2 + topicBytes.Length + dataBytes.Length];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(encoded, (ushort)topicBytes.Length);
            topicBytes.CopyTo(encoded, 2);
            dataBytes.CopyTo(encoded, 2 + topicBytes.Length);
            pipe.onSceneMessage.Invoke(new ISceneCommunicationPipe.DecodedMessage(encoded, senderIdentity, isTrustedSource: true));
        }

        /// <summary>
        /// Test double for ISceneCommunicationPipe.
        /// Needed because ReadOnlySpan cannot be mocked by NSubstitute.
        /// </summary>
        private class TestSceneCommunicationPipe : ISceneCommunicationPipe
        {
            internal readonly List<byte[]> sendMessageCalls = new ();
            internal ISceneCommunicationPipe.SceneMessageHandler onSceneMessage;
            internal string registeredSceneId;
            internal ISceneCommunicationPipe.MsgType registeredMsgType;
            internal bool handlerRemoved;

            public void AddSceneMessageHandler(string sceneId, ISceneCommunicationPipe.MsgType msgType, ISceneCommunicationPipe.SceneMessageHandler onSceneMessage)
            {
                registeredSceneId = sceneId;
                registeredMsgType = msgType;
                this.onSceneMessage = onSceneMessage;
            }

            public void RemoveSceneMessageHandler(string sceneId, ISceneCommunicationPipe.MsgType msgType, ISceneCommunicationPipe.SceneMessageHandler onSceneMessage)
            {
                handlerRemoved = true;
            }

            public void SendMessage(ReadOnlySpan<byte> message, string sceneId, ISceneCommunicationPipe.ConnectivityAssertiveness assertiveness, CancellationToken ct, string specialRecipient = null)
            {
                sendMessageCalls.Add(message.ToArray());
            }

            public void RegisterSceneRoom(string sceneId, DCL.Multiplayer.Connections.Rooms.Connective.IConnectiveRoom room, DCL.Multiplayer.Connections.Messaging.Pipe.IMessagePipe roomPipe) { }

            public void RemoveSceneRoom(string sceneId) { }
        }
    }
}
