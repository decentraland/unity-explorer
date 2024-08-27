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
        public enum MsgType
        {
            String = 1, // SDK scenes MessageBus messages
            Uint8Array = 2,
        }

        protected readonly List<IMemoryOwner<byte>> eventsToProcess = new ();
        private readonly CancellationTokenSource cancellationTokenSource = new ();
        private readonly ISceneCommunicationPipe sceneCommunicationPipe;
        private readonly ISceneData sceneData;
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IJsOperations jsOperations;
        private readonly Action<ISceneCommunicationPipe.SceneMessage> onMessageReceivedCached;

        internal IReadOnlyList<IMemoryOwner<byte>> EventsToProcess => eventsToProcess;

        protected CommunicationsControllerAPIImplementationBase(
            ISceneData sceneData,
            ISceneCommunicationPipe sceneCommunicationPipe,
            IJsOperations jsOperations,
            ISceneStateProvider sceneStateProvider)
        {
            this.sceneData = sceneData;
            this.sceneCommunicationPipe = sceneCommunicationPipe;
            this.jsOperations = jsOperations;
            this.sceneStateProvider = sceneStateProvider;

            onMessageReceivedCached = OnMessageReceived;
        }

        public void Dispose()
        {
            lock (eventsToProcess) { CleanUpReceivedMessages(); }

            cancellationTokenSource.SafeCancelAndDispose();
        }

        public void OnSceneIsCurrentChanged(bool isCurrent)
        {
            if (isCurrent)
                sceneCommunicationPipe.SetSceneMessageHandler(onMessageReceivedCached);
            else
                sceneCommunicationPipe.RemoveSceneMessageHandler(onMessageReceivedCached);
        }

        public object SendBinary(IReadOnlyList<PoolableByteArray> data)
        {
            if (!sceneStateProvider.IsCurrent)
                return jsOperations.ConvertToScriptTypedArrays(Array.Empty<IMemoryOwner<byte>>());

            foreach (PoolableByteArray poolable in data)
                if (poolable.Length > 0)
                    EncodeAndSendMessage(MsgType.Uint8Array, poolable.Memory.Span);

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

        protected void EncodeAndSendMessage(MsgType msgType, ReadOnlySpan<byte> message)
        {
            Span<byte> encodedMessage = stackalloc byte[message.Length + 1];
            encodedMessage[0] = (byte)msgType;
            message.CopyTo(encodedMessage[1..]);
            sceneCommunicationPipe.SendMessage(message, sceneData.SceneEntityDefinition.id!, cancellationTokenSource.Token);
        }

        private static MsgType DecodeMessage(ref ReadOnlySpan<byte> value)
        {
            var msgType = (MsgType)value[0];
            value = value[1..];
            return msgType;
        }

        private void OnMessageReceived(ISceneCommunicationPipe.SceneMessage receivedMessage)
        {
            ReadOnlySpan<byte> decodedMessage = receivedMessage.Data.Span;
            MsgType msgType = DecodeMessage(ref decodedMessage);

            if (decodedMessage.Length == 0)
                return;

            OnMessageReceived(msgType, decodedMessage, receivedMessage.FromWalletId);
        }

        protected abstract void OnMessageReceived(MsgType messageType, ReadOnlySpan<byte> decodedMessage, string fromWalletId);
    }
}
