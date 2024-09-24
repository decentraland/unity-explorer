using CRDT.Memory;
using CrdtEcsBridge.PoolsProviders;
using SceneRunner.Scene;
using SceneRuntime;
using System;
using System.Buffers;
using System.Text;

namespace CrdtEcsBridge.JsModulesImplementation.Communications
{
    public class CommunicationsControllerAPIImplementation : CommunicationsControllerAPIImplementationBase
    {
        private readonly ICRDTMemoryAllocator crdtMemoryAllocator;

        public CommunicationsControllerAPIImplementation(ISceneData sceneData,
            ISceneCommunicationPipe messagePipesHub,
            IJsOperations jsOperations,
            ICRDTMemoryAllocator crdtMemoryAllocator) : base(sceneData,
            messagePipesHub,
            jsOperations, ISceneCommunicationPipe.MsgType.Uint8Array)
        {
            this.crdtMemoryAllocator = crdtMemoryAllocator;
        }

        protected override void OnMessageReceived(ISceneCommunicationPipe.DecodedMessage message)
        {
            // Wallet Id
            int walletBytesCount = Encoding.UTF8.GetByteCount(message.FromWalletId);
            Span<byte> senderBytes = stackalloc byte[walletBytesCount];
            Encoding.UTF8.GetBytes(message.FromWalletId, senderBytes);

            int messageLength = senderBytes.Length + message.Data.Length + 1;

            IMemoryOwner<byte>? serializedMessageOwner = crdtMemoryAllocator.GetMemoryBuffer(messageLength);
            Span<byte> serializedMessage = serializedMessageOwner.Memory.Span;

            serializedMessage[0] = (byte)senderBytes.Length;
            senderBytes.CopyTo(serializedMessage[1..]);
            message.Data.CopyTo(serializedMessage.Slice(senderBytes.Length + 1));

            lock (eventsToProcess) { eventsToProcess.Add(serializedMessageOwner); }
        }
    }
}
