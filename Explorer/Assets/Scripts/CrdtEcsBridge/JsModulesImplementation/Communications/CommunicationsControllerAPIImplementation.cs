using CRDT.Memory;
using CrdtEcsBridge.PoolsProviders;
using DCL.Multiplayer.Connections.Messaging;
using Decentraland.Kernel.Comms.Rfc4;
using SceneRunner.Scene;
using SceneRuntime;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Utility;

namespace CrdtEcsBridge.JsModulesImplementation.Communications
{
    public class CommunicationsControllerAPIImplementation : CommunicationsControllerAPIImplementationBase
    {
        private readonly ICRDTMemoryAllocator crdtMemoryAllocator;

        public CommunicationsControllerAPIImplementation(
            ISceneData sceneData,
            ICommunicationControllerHub messagePipesHub,
            IJsOperations jsOperations,
            ICRDTMemoryAllocator crdtMemoryAllocator,
            ISceneStateProvider sceneStateProvider) : base(
            sceneData,
            messagePipesHub,
            jsOperations,
            sceneStateProvider)
        {
            this.crdtMemoryAllocator = crdtMemoryAllocator;
        }

        public void Dispose()
        {
            lock (eventsToProcess) { CleanUpReceivedMessages(); }

            cancellationTokenSource.SafeCancelAndDispose();
        }

        public void OnSceneIsCurrentChanged(bool isCurrent)
        {
            if (isCurrent)
                messagePipesHub.SetSceneMessageHandler(onMessageReceivedCached);
            else
                messagePipesHub.RemoveSceneMessageHandler(onMessageReceivedCached);
        }

        public object SendBinary(IReadOnlyList<PoolableByteArray> data)
        {
            if (!sceneStateProvider.IsCurrent)
                return jsOperations.ConvertToScriptTypedArrays(Array.Empty<IMemoryOwner<byte>>());

            foreach (var poolable in data)
            {
                if (poolable.Length == 0)
                    continue;

                var message = poolable.Memory;

                EncodeAndSend();

                void EncodeAndSend()
                {
                    Span<byte> encodedMessage = stackalloc byte[message.Length + 1];
                    encodedMessage[0] = (byte)MsgType.Uint8Array;
                    message.Span.CopyTo(encodedMessage[1..]);
                    SendMessage(encodedMessage);
                }
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

        private void SendMessage(ReadOnlySpan<byte> message)
        {
            messagePipesHub.SendMessage(message, sceneData.SceneEntityDefinition.id, cancellationTokenSource.Token);
        }

        protected override void OnMessageReceived(ReceivedMessage<Scene> receivedMessage)
        {
            using (receivedMessage)
            {
                ReadOnlySpan<byte> decodedMessage = receivedMessage.Payload.Data.Span;
                MsgType msgType = DecodeMessage(ref decodedMessage);

                if (msgType != MsgType.Uint8Array || decodedMessage.Length == 0)
                    return;

                // Wallet Id
                int walletBytesCount = Encoding.UTF8.GetByteCount(receivedMessage.FromWalletId);
                Span<byte> senderBytes = stackalloc byte[walletBytesCount];
                Encoding.UTF8.GetBytes(receivedMessage.FromWalletId, senderBytes);

                int messageLength = senderBytes.Length + decodedMessage.Length + 1;

                IMemoryOwner<byte>? serializedMessageOwner = crdtMemoryAllocator.GetMemoryBuffer(messageLength);
                Span<byte> serializedMessage = serializedMessageOwner.Memory.Span;

                serializedMessage[0] = (byte)senderBytes.Length;
                senderBytes.CopyTo(serializedMessage[1..]);
                decodedMessage.CopyTo(serializedMessage.Slice(senderBytes.Length + 1));

                lock (eventsToProcess) { eventsToProcess.Add(serializedMessageOwner); }
            }
        }
    }
}
