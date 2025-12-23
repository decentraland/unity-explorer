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
        protected readonly IJsOperations jsOperations;
        private readonly ISceneCommunicationPipe.MsgType typeToHandle;
        private readonly ScriptObject eventArray;
        private readonly ISceneCommunicationPipe.SceneMessageHandler onMessageReceivedCached;
        private readonly ISceneData sceneData;

        internal IReadOnlyList<ITypedArray<byte>> EventsToProcess => eventsToProcess;

        protected CommunicationsControllerAPIImplementationBase(
            ISceneData sceneData,
            ISceneCommunicationPipe sceneCommunicationPipe,
            IJsOperations jsOperations,
            ISceneCommunicationPipe.MsgType typeToHandle)
        {
            this.sceneData = sceneData;
            this.sceneCommunicationPipe = sceneCommunicationPipe;
            this.jsOperations = jsOperations;
            this.typeToHandle = typeToHandle;
            eventArray = jsOperations.NewArray();
            onMessageReceivedCached = OnMessageReceived;

            this.sceneCommunicationPipe.AddSceneMessageHandler(sceneId, typeToHandle, onMessageReceivedCached);
        }

        private string sceneId => sceneData.SceneEntityDefinition.id!;

        public void Dispose()
        {
            sceneCommunicationPipe.RemoveSceneMessageHandler(sceneId, typeToHandle, onMessageReceivedCached);
            cancellationTokenSource.SafeCancelAndDispose();
        }

        public void SendBinary(IEnumerable<ITypedArray<byte>> broadcastData, string? recipient = null)
        {
            // Authoritative multiplayer enforces sending messages to the special peer
            if (sceneData.SceneEntityDefinition.metadata.authoritativeMultiplayer)
                recipient = "authoritative-server";

            foreach (ITypedArray<byte> data in broadcastData)
                if (data.Length > 0)
                {
                    ulong length = data.Length;
                    var instance = this;

                    data.InvokeWithDirectAccess(
                        static (ptr, args) => {
                            Span<byte> span;
                            unsafe
                            {
                                span = new Span<byte>(ptr.ToPointer(), (int) args.length);
                            }

                            byte firstByte = span[0];

                            ISceneCommunicationPipe.ConnectivityAssertiveness assertiveness = firstByte == (int)CommsMessageType.REQ_CRDT_STATE
                                ? ISceneCommunicationPipe.ConnectivityAssertiveness.DELIVERY_ASSERTED
                                : ISceneCommunicationPipe.ConnectivityAssertiveness.DROP_IF_NOT_CONNECTED;

                            int length = EncodedMessage.LengthWithReservedByte(span.Length);
                            Span<byte> contentAlloc = stackalloc byte[length];
                            EncodedMessage encodedMessage = new EncodedMessage(contentAlloc);
                            encodedMessage.AssignType(ISceneCommunicationPipe.MsgType.Uint8Array);

                            // Filter CRDT messages before sending
                            if (firstByte == (int)CommsMessageType.CRDT)
                                encodedMessage.FilterBy(CRDTFilter.FilterSceneMessageBatch, span);
                            // Filter RES_CRDT_STATE messages before sending
                            else if (firstByte == (int)CommsMessageType.RES_CRDT_STATE)
                                encodedMessage.FilterBy(CRDTFilter.FilterCRDTState, span);

                            args.instance.EncodeAndSendMessage(encodedMessage, assertiveness, args.recipient);
                        }, 
                        (instance, recipient, length)
                    );


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

        protected void EncodeAndSendMessage(EncodedMessage encodedMessage, ISceneCommunicationPipe.ConnectivityAssertiveness assertivenes, string? specialRecipient)
        {
            sceneCommunicationPipe.SendMessage(encodedMessage.ContentWithHeader(), sceneId, assertivenes, cancellationTokenSource.Token, specialRecipient);
        }

        protected abstract void OnMessageReceived(ISceneCommunicationPipe.DecodedMessage decodedMessage);


        public delegate void SpanFilter(ReadOnlySpan<byte> src, Span<byte> dst, out int writtenBytes);

        protected ref struct EncodedMessage
        {
            public const int RESERVED_SIZE = 1;

            private Span<byte> data;
            private int contentLength;

            // Be sure to reserve 1 byte for the msg type
            public EncodedMessage(Span<byte> data)
            {
                this.data = data; // Extra byte for message type
                contentLength = data.Length - RESERVED_SIZE;
            }

            public static int LengthWithReservedByte(int length)
            {
                return length + RESERVED_SIZE;
            }

            public Span<byte> Content()
            {
                return data.Slice(RESERVED_SIZE, contentLength); // first byte is msg type
            }

            public Span<byte> ContentWithHeader()
            {
                return data.Slice(0, contentLength + RESERVED_SIZE); // first byte is msg type
            }

            public void ResizeContent(int length)
            {
                int targetTotalLength = length + RESERVED_SIZE;
                UnityEngine.Assertions.Assert.IsFalse(
                    targetTotalLength > data.Length, 
                    "Cannot resize to target length greater than the origin span"
                );

                contentLength = length;
            }

            public void FilterBy(SpanFilter filter, ReadOnlySpan<byte> src)
            {
                Span<byte> content = Content();
                filter(src, content, out int newContentLength);
                ResizeContent(newContentLength);
            }

            public void AssignType(ISceneCommunicationPipe.MsgType msgType)
            {
                data[0] = (byte)msgType;
            }
        }
    }
}
