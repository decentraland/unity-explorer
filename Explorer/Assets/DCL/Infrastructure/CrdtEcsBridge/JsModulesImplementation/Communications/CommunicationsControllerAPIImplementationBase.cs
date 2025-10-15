using CRDT;
using CrdtEcsBridge.PoolsProviders;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using SceneRunner.Scene;
using SceneRuntime;
using SceneRuntime.Apis.Modules.CommunicationsControllerApi;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;

namespace CrdtEcsBridge.JsModulesImplementation.Communications
{
    public abstract class CommunicationsControllerAPIImplementationBase : ICommunicationsControllerAPI
    {
        // Must be aligned with SDK runtime 1st-byte values at:
        // https://github.com/decentraland/js-sdk-toolchain/blob/c8695cd9b94e87ad567520089969583d9d36637f/packages/@dcl/sdk/src/network/binary-message-bus.ts#L3-L7
        enum CommsMessageType {
          CRDT = 1,
          REQ_CRDT_STATE = 2,   // Special signal to receive CRDT State from a peer
          RES_CRDT_STATE = 3    // Special signal to send CRDT State to a peer
        }

        protected readonly List<ITypedArray<byte>> eventsToProcess = new ();
        private readonly CancellationTokenSource cancellationTokenSource = new ();
        private readonly ISceneCommunicationPipe sceneCommunicationPipe;
        private readonly string sceneId;
        protected readonly IJsOperations jsOperations;
        private readonly ISceneCommunicationPipe.MsgType typeToHandle;
        private readonly ScriptObject eventArray;
        private readonly ISceneCommunicationPipe.SceneMessageHandler onMessageReceivedCached;

        internal IReadOnlyList<ITypedArray<byte>> EventsToProcess => eventsToProcess;

        protected CommunicationsControllerAPIImplementationBase(
            ISceneData sceneData,
            ISceneCommunicationPipe sceneCommunicationPipe,
            IJsOperations jsOperations,
            ISceneCommunicationPipe.MsgType typeToHandle)
        {
            sceneId = sceneData.SceneEntityDefinition.id!;
            this.sceneCommunicationPipe = sceneCommunicationPipe;
            this.jsOperations = jsOperations;
            this.typeToHandle = typeToHandle;
            eventArray = jsOperations.NewArray();
            onMessageReceivedCached = OnMessageReceived;

            this.sceneCommunicationPipe.AddSceneMessageHandler(sceneId, typeToHandle, onMessageReceivedCached);
        }

        public void Dispose()
        {
            sceneCommunicationPipe.RemoveSceneMessageHandler(sceneId, typeToHandle, onMessageReceivedCached);
            cancellationTokenSource.SafeCancelAndDispose();
        }

        public void SendBinary(IReadOnlyList<PoolableByteArray> broadcastData, string? recipient = null)
        {
            foreach (PoolableByteArray poolable in broadcastData)
                if (poolable.Length > 0)
                {
                    byte firstByte = poolable.Span[0];
                    ISceneCommunicationPipe.ConnectivityAssertiveness assertiveness = firstByte == (int)CommsMessageType.REQ_CRDT_STATE
                        ? ISceneCommunicationPipe.ConnectivityAssertiveness.DELIVERY_ASSERTED
                        : ISceneCommunicationPipe.ConnectivityAssertiveness.DROP_IF_NOT_CONNECTED;

                    EncodeAndSendMessage(ISceneCommunicationPipe.MsgType.Uint8Array, poolable.Memory.Span, assertiveness, recipient, useFilter: firstByte == (int)CommsMessageType.CRDT);
                }
        }

        public ScriptObject GetResult()
        {
            lock (eventsToProcess)
            {
                eventArray.SetProperty("length", eventsToProcess.Count);

                for (int i = 0; i < eventsToProcess.Count; i++)
                    eventArray.SetProperty(i, eventsToProcess[i]);

                eventsToProcess.Clear();
                return eventArray;
            }
        }

        protected void EncodeAndSendMessage(ISceneCommunicationPipe.MsgType msgType, ReadOnlySpan<byte> message, ISceneCommunicationPipe.ConnectivityAssertiveness assertivenes, string? specialRecipient, bool useFilter)
        {
            Span<byte> filtered = stackalloc byte[message.Length];

            if (useFilter)
            {
                CRDTFilter.FilterSceneMessageBatch(message, filtered, out int totalWrite);
                filtered = filtered.Slice(0, totalWrite);
            }
            else
            {
                message.CopyTo(filtered);
            }

            Span<byte> encodedMessage = stackalloc byte[filtered.Length + 1];
            encodedMessage[0] = (byte)msgType;
            filtered.CopyTo(encodedMessage[1..]);

            sceneCommunicationPipe.SendMessage(encodedMessage, sceneId, assertivenes, cancellationTokenSource.Token, specialRecipient);
        }

        protected abstract void OnMessageReceived(ISceneCommunicationPipe.DecodedMessage decodedMessage);
    }
}
