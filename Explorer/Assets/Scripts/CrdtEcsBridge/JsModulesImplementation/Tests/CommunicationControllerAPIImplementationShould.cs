using CRDT.Memory;
using CrdtEcsBridge.JsModulesImplementation.Communications;
using CrdtEcsBridge.PoolsProviders;
using DCL.Ipfs;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using Decentraland.Kernel.Comms.Rfc4;
using Google.Protobuf;
using LiveKit.Internal.FFIClients.Pools;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using SceneRuntime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace CrdtEcsBridge.JsModulesImplementation.Tests
{
    public class CommunicationControllerAPIImplementationShould
    {
        private CommunicationsControllerAPIImplementation api;
        private TestCommunicationControllerHub communicationControllerHub;
        private IMessagePipe messagePipe;
        private IJsOperations jsOperations;
        private ICRDTMemoryAllocator crdtMemoryAllocator;

        [SetUp]
        public void SetUp()
        {
            communicationControllerHub = new TestCommunicationControllerHub();

            ISceneData sceneData = Substitute.For<ISceneData>();
            sceneData.SceneEntityDefinition.Returns(new SceneEntityDefinition { id = "TEST_SCENE" });

            crdtMemoryAllocator = CRDTOriginalMemorySlicer.Create();
            var sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);

            api = new CommunicationsControllerAPIImplementation(sceneData, communicationControllerHub, jsOperations = Substitute.For<IJsOperations>(), crdtMemoryAllocator, sceneStateProvider);
            api.OnSceneBecameCurrent();
        }

        [Test]
        public void SendBinary([Range(0, 5)] int outerArraySize, [Range(1, 500, 50)] int innerArraySize)
        {
            // Generate random array of arrays

            var outerArray = new PoolableByteArray[outerArraySize];

            for (var i = 0; i < outerArraySize; i++)
                outerArray[i] = new PoolableByteArray(GetRandomBytes(innerArraySize), innerArraySize, null);

            api.SendBinary(outerArray);

            var expectedCalls = outerArray.Select(o => o.Prepend((byte)CommunicationsControllerAPIImplementation.MsgType.Uint8Array).ToArray()).ToList();

            // Assert the 2d array is equal
            CollectionAssert.AreEqual(expectedCalls, communicationControllerHub.sendMessageCalls);

            // Assert JSOperations called
            jsOperations.Received().ConvertToScriptTypedArrays(api.EventsToProcess);
        }

        [Test]
        public void OnMessageReceived()
        {
            const string WALLET_ID = "0x71C7656EC7ab88b098defB751B7401B5f6d8976F";
            const string SCENE_ID = "TEST_SCENE";

            byte[] data = GetRandomBytes(50).Prepend((byte)CommunicationsControllerAPIImplementation.MsgType.Uint8Array).ToArray();

            var receivedMessage = new ReceivedMessage<Scene>(new Scene { Data = ByteString.CopyFrom(data), SceneId = SCENE_ID }, new Packet(), WALLET_ID, Substitute.For<IMultiPool>());
            communicationControllerHub.onSceneMessage.Invoke(receivedMessage);

            byte[] walletBytes = Encoding.UTF8.GetBytes(receivedMessage.FromWalletId);

            IEnumerable<byte> expectedMessage =
                walletBytes.Concat(data.Skip(1))
                           .Prepend((byte)walletBytes.Length);

            // Check events to process
            Assert.AreEqual(1, api.EventsToProcess.Count);
            CollectionAssert.AreEqual(expectedMessage, api.EventsToProcess[0].Memory.ToArray());
        }

        private static byte[] GetRandomBytes(int size)
        {
            var rand = new Random();
            var buf = new byte[size];
            rand.NextBytes(buf);
            return buf;
        }

        [TearDown]
        public void TearDown()
        {
            api.Dispose();
        }

        // This class exists because we can't mock ReadOnlySpan (ref structs)
        private class TestCommunicationControllerHub : ICommunicationControllerHub
        {
            internal readonly List<byte[]> sendMessageCalls = new ();
            internal Action<ReceivedMessage<Scene>> onSceneMessage;

            public void SetSceneMessageHandler(Action<ReceivedMessage<Scene>> onSceneMessage)
            {
                this.onSceneMessage = onSceneMessage;
            }

            public void SendMessage(ReadOnlySpan<byte> message, string sceneId, CancellationToken ct)
            {
                sendMessageCalls.Add(message.ToArray());
            }
        }
    }
}
