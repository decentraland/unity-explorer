﻿using CrdtEcsBridge.JsModulesImplementation.Communications;
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

            jsOperations.GetTempUint8Array().Returns(
                x => uint8ArrayCtor.Invoke(true, IJsOperations.LIVEKIT_MAX_SIZE));

            jsOperations.NewArray().Returns(x => arrayCtor.Invoke(true));

            api = new CommunicationsControllerAPIImplementation(sceneData, sceneCommunicationPipe,
                jsOperations);
        }

        [Test]
        public void SendBinary([Range(0, 5)] int outerArraySize, [Range(1, 500, 50)] int innerArraySize)
        {
            // Generate random array of arrays

            var outerArray = new PoolableByteArray[outerArraySize];

            for (var i = 0; i < outerArraySize; i++)
                outerArray[i] = new PoolableByteArray(GetRandomBytes(innerArraySize), innerArraySize, null);

            api.SendBinary(outerArray);
            api.GetResult();

            var expectedCalls = outerArray.Select(o => o.Prepend((byte)ISceneCommunicationPipe.MsgType.Uint8Array).ToArray()).ToList();

            // Assert the 2d array is equal
            CollectionAssert.AreEqual(expectedCalls, sceneCommunicationPipe.sendMessageCalls);

            // Assert JSOperations called
            jsOperations.Received(1).GetTempUint8Array();
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
