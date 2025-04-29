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
        /// <summary>
        ///     Special signal to receive CRDT State from a peer
        /// </summary>
        private const byte REQ_CRDT_STATE = 2;

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
                    ISceneCommunicationPipe.ConnectivityAssertiveness assertiveness = poolable.Span[0] == REQ_CRDT_STATE
                        ? ISceneCommunicationPipe.ConnectivityAssertiveness.DELIVERY_ASSERTED
                        : ISceneCommunicationPipe.ConnectivityAssertiveness.DROP_IF_NOT_CONNECTED;

                    EncodeAndSendMessage(ISceneCommunicationPipe.MsgType.Uint8Array, poolable.Memory.Span, assertiveness, recipient);
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
