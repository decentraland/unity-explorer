using CRDT.Memory;
using CrdtEcsBridge.PoolsProviders;
using SceneRunner.Scene;
using SceneRuntime;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
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

        protected override void OnMessageReceived(ICommunicationControllerHub.SceneMessage receivedMessage)
        {
            ReadOnlySpan<byte> decodedMessage = receivedMessage.Data.Span;
            MsgType msgType = DecodeMessage(ref decodedMessage);

            if (msgType != MsgType.Uint8Array || decodedMessage.Length == 0)
                return;

            // Wallet Id
            int walletBytesCount = Encoding.UTF8.GetByteCount(receivedMessage.WalletId);
            Span<byte> senderBytes = stackalloc byte[walletBytesCount];
            Encoding.UTF8.GetBytes(receivedMessage.WalletId, senderBytes);

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
