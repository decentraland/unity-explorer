using CRDT.Memory;
using CrdtEcsBridge.PoolsProviders;
using ECS;
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

        public CommunicationsControllerAPIImplementation(
            IRealmData realmData,
            ISceneData sceneData,
            ISceneCommunicationPipe messagePipesHub,
            IJsOperations jsOperations,
            ICRDTMemoryAllocator crdtMemoryAllocator,
            ISceneStateProvider sceneStateProvider) : base(
            realmData,
            sceneData,
            messagePipesHub,
            jsOperations,
            sceneStateProvider)
        {
            this.crdtMemoryAllocator = crdtMemoryAllocator;
        }

        protected override void OnMessageReceived(MsgType messageType, ReadOnlySpan<byte> decodedMessage, string fromWalletId)
        {
            if (messageType != MsgType.Uint8Array)
                return;

            // Wallet Id
            int walletBytesCount = Encoding.UTF8.GetByteCount(fromWalletId);
            Span<byte> senderBytes = stackalloc byte[walletBytesCount];
            Encoding.UTF8.GetBytes(fromWalletId, senderBytes);

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
