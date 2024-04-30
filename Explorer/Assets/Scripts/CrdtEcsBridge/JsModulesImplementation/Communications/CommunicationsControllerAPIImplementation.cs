using CRDT.Memory;
using CrdtEcsBridge.PoolsProviders;
using DCL.Diagnostics;
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
        internal enum MsgType
        {
            String = 1, // Deprecated in SDK7
            Uint8Array = 2,
        }

        private readonly CancellationTokenSource cancellationTokenSource = new ();
        private readonly List<IMemoryOwner<byte>> eventsToProcess = new ();
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IJsOperations jsOperations;
        private readonly ICommunicationControllerHub messagePipesHub;
        private readonly ICRDTMemoryAllocator crdtMemoryAllocator;
        private readonly ISceneData sceneData;

        internal IReadOnlyList<IMemoryOwner<byte>> EventsToProcess => eventsToProcess;

        private readonly Action<ReceivedMessage<Scene>> onMessageReceivedCached;

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

            onMessageReceivedCached = OnMessageReceived;
        }

        public void Dispose()
        {
            lock (eventsToProcess) { CleanUpReceivedMessages(); }

            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
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

        public void Send(byte[] data)
        {
            // if (!sceneStateProvider.IsCurrent)
            //     return jsOperations.ConvertToScriptTypedArrays(Array.Empty<IMemoryOwner<byte>>());

            EncodeAndSend();

            void EncodeAndSend()
            {
                Span<byte> encodedMessage = stackalloc byte[data.Length + 1];
                encodedMessage[0] = (byte)MsgType.String;
                data.CopyTo(encodedMessage[1..]);
                SendMessage(encodedMessage);
            }

            // lock (eventsToProcess)
            // {
            //     object result = jsOperations.ConvertToScriptTypedArrays(eventsToProcess);
            //     CleanUpReceivedMessages();
            //
            //     return result;
            // }
        }

        private void CleanUpReceivedMessages()
        {
            foreach (IMemoryOwner<byte>? message in eventsToProcess)
                message.Dispose();

            eventsToProcess.Clear();
        }

        private void SendMessage(ReadOnlySpan<byte> message)
        {
            UnityEngine.Debug.Log($"PRAVS - SendMessage - ID: {sceneData.SceneEntityDefinition.id}");
            messagePipesHub.SendMessage(message, sceneData.SceneEntityDefinition.id, cancellationTokenSource.Token);
        }

        private void OnMessageReceived(ReceivedMessage<Scene> receivedMessage)
        {
            using (receivedMessage)
            {
                ReadOnlySpan<byte> decodedMessage = receivedMessage.Payload.Data.Span;
                MsgType msgType = DecodeMessage(ref decodedMessage);

                UnityEngine.Debug.Log($"PRAVS - OnMessageReceived - 1");

                // TODO: Receive MsgType.String
                if (decodedMessage.Length == 0)
                    return;

                if (msgType == MsgType.String)
                {
                    UnityEngine.Debug.Log($"PRAVS - OnMessageReceived - 2");
                    return;
                }

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

        private static MsgType DecodeMessage(ref ReadOnlySpan<byte> value)
        {
            var msgType = (MsgType)value[0];
            value = value[1..];
            return msgType;
        }
    }
}
