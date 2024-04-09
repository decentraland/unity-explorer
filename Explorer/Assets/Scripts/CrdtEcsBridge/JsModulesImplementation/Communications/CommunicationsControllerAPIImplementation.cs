using CRDT.Memory;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using Decentraland.Kernel.Comms.Rfc4;
using Google.Protobuf;
using JetBrains.Annotations;
using SceneRunner.Scene;
using SceneRuntime;
using SceneRuntime.Apis.Modules.CommunicationsControllerApi;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace CrdtEcsBridge.CommunicationsController
{
    public class CommunicationsControllerAPIImplementation : ICommunicationsControllerAPI
    {
        private readonly CancellationTokenSource cancellationTokenSource = new ();
        private readonly List<IMemoryOwner<byte>> eventsToProcess = new ();
        private readonly IJsOperations jsOperations;
        private readonly ICommunicationControllerHub messagePipesHub;
        private readonly ICRDTMemoryAllocator crdtMemoryAllocator;
        private readonly ISceneData sceneData;

        public CommunicationsControllerAPIImplementation(
            ISceneData sceneData,
            ICommunicationControllerHub messagePipesHub,
            IJsOperations jsOperations,
            ICRDTMemoryAllocator crdtMemoryAllocator)
        {
            this.sceneData = sceneData;
            this.messagePipesHub = messagePipesHub;
            this.jsOperations = jsOperations;
            this.crdtMemoryAllocator = crdtMemoryAllocator;
        }

        public void OnSceneBecameCurrent()
        {
            messagePipesHub.SetSceneMessageHandler(OnMessageReceived);
        }

        public object SendBinary(IReadOnlyList<byte[]> data)
        {
            foreach (byte[] message in data)
            {
                if (message.Length == 0)
                    continue;

                byte[] encodedMessage = EncodeMessage(message, MsgType.Uint8Array);
                SendMessage(encodedMessage);
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

                int messageLength = senderBytes.Length + decodedMessage.Length + 1;

                var serializedMessageOwner = crdtMemoryAllocator.GetMemoryBuffer(messageLength);
                var serializedMessage = serializedMessageOwner.Memory.Span;

                serializedMessage[0] = (byte)senderBytes.Length;
                senderBytes.CopyTo(serializedMessage[1..]);
                decodedMessage.CopyTo(serializedMessage.Slice(senderBytes.Length + 1));

                eventsToProcess.Add(serializedMessageOwner);
            }
        }

        private static byte[] EncodeMessage(byte[] data, MsgType type)
        {
            var message = new byte[data.Length + 1];
            message[0] = (byte)type;
            Array.Copy(data, 0, message, 1, data.Length);
            return message;
        }

        private static MsgType DecodeMessage(ref ReadOnlySpan<byte> value)
        {
            var msgType = (MsgType)value[0];
            value = value[1..];
            return msgType;
        }

        private enum MsgType
        {
            String = 1, // Deprecated in SDK7
            Uint8Array = 2,
        }
    }
}
