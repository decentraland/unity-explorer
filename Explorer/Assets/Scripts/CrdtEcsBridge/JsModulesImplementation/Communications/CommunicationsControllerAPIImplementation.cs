using CRDT.Memory;
using DCL.Multiplayer.Connections.Messaging;
using Decentraland.Kernel.Comms.Rfc4;
using SceneRunner.Scene;
using SceneRuntime;
using SceneRuntime.Apis.Modules.CommunicationsControllerApi;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace CrdtEcsBridge.JsModulesImplementation.Communications
{
    public class CommunicationsControllerAPIImplementation : ICommunicationsControllerAPI
    {
        private readonly CancellationTokenSource cancellationTokenSource = new ();
        private readonly List<IMemoryOwner<byte>> eventsToProcess = new ();
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IJsOperations jsOperations;
        private readonly ICommunicationControllerHub messagePipesHub;
        private readonly ICRDTMemoryAllocator crdtMemoryAllocator;
        private readonly ISceneData sceneData;

        public CommunicationsControllerAPIImplementation(
            ISceneData sceneData,
            ICommunicationControllerHub messagePipesHub,
            IJsOperations jsOperations,
            ICRDTMemoryAllocator crdtMemoryAllocator,
            ISceneStateProvider sceneStateProvider)
        {
            this.sceneData = sceneData;
            this.messagePipesHub = messagePipesHub;
            this.jsOperations = jsOperations;
            this.crdtMemoryAllocator = crdtMemoryAllocator;
            this.sceneStateProvider = sceneStateProvider;
        }

        internal IReadOnlyList<IMemoryOwner<byte>> EventsToProcess => eventsToProcess;

        public void OnSceneBecameCurrent()
        {
            messagePipesHub.SetSceneMessageHandler(OnMessageReceived);
        }

        public object SendBinary(IReadOnlyList<byte[]> data)
        {
            if (!sceneStateProvider.IsCurrent)
                return jsOperations.ConvertToScriptTypedArrays(Array.Empty<IMemoryOwner<byte>>());

            foreach (byte[] message in data)
            {
                if (message.Length == 0)
                    continue;

                EncodeAndSend();

                void EncodeAndSend()
                {
                    Span<byte> encodedMessage = stackalloc byte[message.Length + 1];
                    encodedMessage[0] = (byte)MsgType.Uint8Array;
                    message.CopyTo(encodedMessage[1..]);
                    SendMessage(encodedMessage);
                }
            }

            object result = jsOperations.ConvertToScriptTypedArrays(eventsToProcess);

            CleanUpReceivedMessages();

            return result;
        }

        public void Dispose()
        {
            CleanUpReceivedMessages();

            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }

        private void CleanUpReceivedMessages()
        {
            foreach (var message in eventsToProcess)
                message.Dispose();

            eventsToProcess.Clear();
        }

        private void SendMessage(ReadOnlySpan<byte> message)
        {
            messagePipesHub.SendMessage(message, sceneData.SceneEntityDefinition.id, cancellationTokenSource.Token);
        }

        private void OnMessageReceived(ReceivedMessage<Scene> receivedMessage)
        {
            using (receivedMessage)
            {
                var decodedMessage = receivedMessage.Payload.Data.Span;
                MsgType msgType = DecodeMessage(ref decodedMessage);

                if (msgType != MsgType.Uint8Array || decodedMessage.Length == 0)
                    return;

                // Wallet Id
                int walletBytesCount = Encoding.UTF8.GetByteCount(receivedMessage.FromWalletId);
                Span<byte> senderBytes = stackalloc byte[walletBytesCount];
                Encoding.UTF8.GetBytes(receivedMessage.FromWalletId, senderBytes);

                int messageLength = senderBytes.Length + decodedMessage.Length + 1;

                var serializedMessageOwner = crdtMemoryAllocator.GetMemoryBuffer(messageLength);
                var serializedMessage = serializedMessageOwner.Memory.Span;

                serializedMessage[0] = (byte)senderBytes.Length;
                senderBytes.CopyTo(serializedMessage[1..]);
                decodedMessage.CopyTo(serializedMessage.Slice(senderBytes.Length + 1));

                eventsToProcess.Add(serializedMessageOwner);
            }
        }

        private static MsgType DecodeMessage(ref ReadOnlySpan<byte> value)
        {
            var msgType = (MsgType)value[0];
            value = value[1..];
            return msgType;
        }

        internal enum MsgType
        {
            String = 1, // Deprecated in SDK7
            Uint8Array = 2,
        }
    }
}
