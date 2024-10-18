using CrdtEcsBridge.PoolsProviders;
using SceneRunner.Scene;
using SceneRuntime;
using SceneRuntime.Apis.Modules.CommunicationsControllerApi;
using System;
using System.Buffers;
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

        protected readonly List<IMemoryOwner<byte>> eventsToProcess = new ();
        private readonly CancellationTokenSource cancellationTokenSource = new ();
        private readonly ISceneCommunicationPipe sceneCommunicationPipe;
        private readonly string sceneId;
        private readonly IJsOperations jsOperations;
        private readonly ISceneCommunicationPipe.MsgType typeToHandle;

        private readonly ISceneCommunicationPipe.SceneMessageHandler onMessageReceivedCached;

        internal IReadOnlyList<IMemoryOwner<byte>> EventsToProcess => eventsToProcess;

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

            onMessageReceivedCached = OnMessageReceived;

            this.sceneCommunicationPipe.AddSceneMessageHandler(sceneId, typeToHandle, onMessageReceivedCached);
        }

        public void Dispose()
        {
            sceneCommunicationPipe.RemoveSceneMessageHandler(sceneId, typeToHandle, onMessageReceivedCached);

            lock (eventsToProcess) { CleanUpReceivedMessages(); }

            cancellationTokenSource.SafeCancelAndDispose();
        }

        public object SendBinary(IReadOnlyList<PoolableByteArray> data)
        {
            foreach (PoolableByteArray poolable in data)
                if (poolable.Length > 0)
                {
                    ISceneCommunicationPipe.ConnectivityAssertiveness assertiveness = poolable.Span[0] == REQ_CRDT_STATE
                        ? ISceneCommunicationPipe.ConnectivityAssertiveness.DELIVERY_ASSERTED
                        : ISceneCommunicationPipe.ConnectivityAssertiveness.DROP_IF_NOT_CONNECTED;

                    EncodeAndSendMessage(ISceneCommunicationPipe.MsgType.Uint8Array, poolable.Memory.Span, assertiveness);
                }


            lock (eventsToProcess)
            {
                object result = jsOperations.ConvertToScriptTypedArrays(eventsToProcess);
                CleanUpReceivedMessages();

                return result;
            }
        }

        private void CleanUpReceivedMessages()
        {
            foreach (IMemoryOwner<byte>? message in eventsToProcess)
                message.Dispose();

            eventsToProcess.Clear();
        }

        protected void EncodeAndSendMessage(ISceneCommunicationPipe.MsgType msgType, ReadOnlySpan<byte> message, ISceneCommunicationPipe.ConnectivityAssertiveness assertiveness)
        {
            Span<byte> encodedMessage = stackalloc byte[message.Length + 1];
            encodedMessage[0] = (byte)msgType;
            message.CopyTo(encodedMessage[1..]);

            sceneCommunicationPipe.SendMessage(encodedMessage, sceneId, assertiveness, cancellationTokenSource.Token);
        }

        protected abstract void OnMessageReceived(ISceneCommunicationPipe.DecodedMessage decodedMessage);
    }
}
