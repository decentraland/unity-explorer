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

        protected readonly List<PoolableByteArray> eventsToProcess = new ();
        private readonly CancellationTokenSource cancellationTokenSource = new ();
        private readonly ISceneCommunicationPipe sceneCommunicationPipe;
        protected readonly IJsOperations jsOperations;
        private readonly ISceneCommunicationPipe.MsgType typeToHandle;
        private readonly ScriptObject eventArray;
        private readonly ISceneCommunicationPipe.SceneMessageHandler onMessageReceivedCached;
        private readonly ISceneData sceneData;

        internal IReadOnlyList<PoolableByteArray> EventsToProcess => eventsToProcess;

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

            // Register event preparation to run before each JS update.
            // GetResult() is called by JavaScript during V8 execution; building the JS array
            // (SetProperty, InvokeMethod("subarray")) from inside a host callback is re-entrant and
            // corrupts V8's internal EphemeronRememberedSet. Pre-building outside the execution window fixes this.
            jsOperations.AddPreUpdateAction(BuildResultArray);

            this.sceneCommunicationPipe.AddSceneMessageHandler(sceneId, typeToHandle, onMessageReceivedCached);
        }

        private string sceneId => sceneData.SceneEntityDefinition.id!;

        public void Dispose()
        {
            sceneCommunicationPipe.RemoveSceneMessageHandler(sceneId, typeToHandle, onMessageReceivedCached);
            cancellationTokenSource.SafeCancelAndDispose();
        }

        public void SendBinary(IReadOnlyList<PoolableByteArray> broadcastData, string? recipient = null)
        {
            // Authoritative multiplayer enforces sending messages to the special peer
            if (sceneData.SceneEntityDefinition.metadata.authoritativeMultiplayer)
                recipient = "authoritative-server";

            foreach (PoolableByteArray poolable in broadcastData)
                if (poolable.Length > 0)
                {
                    byte firstByte = poolable.Span[0];

                    ISceneCommunicationPipe.ConnectivityAssertiveness assertiveness = firstByte == (int)CommsMessageType.REQ_CRDT_STATE
                        ? ISceneCommunicationPipe.ConnectivityAssertiveness.DELIVERY_ASSERTED
                        : ISceneCommunicationPipe.ConnectivityAssertiveness.DROP_IF_NOT_CONNECTED;

                    // Filter CRDT messages before sending
                    if (firstByte == (int)CommsMessageType.CRDT)
                    {
                        Span<byte> filtered = stackalloc byte[poolable.Memory.Span.Length];
                        int filteredLength = FilterCRDTMessage(poolable.Memory.Span, filtered);
                        EncodeAndSendMessage(ISceneCommunicationPipe.MsgType.Uint8Array, filtered.Slice(0, filteredLength), assertiveness, recipient);
                        continue;
                    }

                    // Filter RES_CRDT_STATE messages before sending
                    if (firstByte == (int)CommsMessageType.RES_CRDT_STATE)
                    {
                        Span<byte> filtered = stackalloc byte[poolable.Memory.Span.Length];
                        int filteredLength = FilterCRDTStateMessage(poolable.Memory.Span, filtered);
                        EncodeAndSendMessage(ISceneCommunicationPipe.MsgType.Uint8Array, filtered.Slice(0, filteredLength), assertiveness, recipient);
                        continue;
                    }

                    EncodeAndSendMessage(ISceneCommunicationPipe.MsgType.Uint8Array, poolable.Memory.Span, assertiveness, recipient);
                }
        }

        /// <summary>
        /// Called by JavaScript during scene update to retrieve queued events.
        /// The array is already populated by <see cref="BuildResultArray"/> which ran before JS execution started.
        /// </summary>
        public ScriptObject GetResult() =>
            eventArray;

        /// <summary>
        /// Builds the JS event array from queued C# events. Must be called before V8 starts executing JS
        /// (registered via <see cref="IJsOperations.AddPreUpdateAction"/>).
        /// All V8 operations here (SetProperty, InvokeMethod) are safe because V8 is not yet executing.
        /// </summary>
        private void BuildResultArray()
        {
            // VALIDATION STUB: discard all events without touching V8.
            // Comms is broken but if the crash stops this proves the EphemeronHashTable
            // corruption originates from the TypedArray view creation in the real implementation.
            lock (eventsToProcess)
            {
                foreach (var e in eventsToProcess) e.Dispose();
                eventsToProcess.Clear();
            }
            eventArray.SetProperty("length", 0);
        }

        private static int FilterCRDTMessage(ReadOnlySpan<byte> message, Span<byte> output)
        {
            CRDTFilter.FilterSceneMessageBatch(message, output, out int totalWrite);
            return totalWrite;
        }

        private static int FilterCRDTStateMessage(ReadOnlySpan<byte> message, Span<byte> output)
        {
            CRDTFilter.FilterCRDTState(message, output, out int totalWrite);
            return totalWrite;
        }

        protected void EncodeAndSendMessage(ISceneCommunicationPipe.MsgType msgType, ReadOnlySpan<byte> message, ISceneCommunicationPipe.ConnectivityAssertiveness assertivenes, string? specialRecipient)
        {
            Span<byte> encodedMessage = stackalloc byte[message.Length + 1];
            encodedMessage[0] = (byte)msgType;
            message.CopyTo(encodedMessage[1..]);

            sceneCommunicationPipe.SendMessage(encodedMessage, sceneId, assertivenes, cancellationTokenSource.Token, specialRecipient);
        }

        protected abstract void OnMessageReceived(ISceneCommunicationPipe.DecodedMessage decodedMessage);
    }
}
