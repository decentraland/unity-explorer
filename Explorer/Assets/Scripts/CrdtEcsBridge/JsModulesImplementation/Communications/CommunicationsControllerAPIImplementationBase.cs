using CrdtEcsBridge.PoolsProviders;
using DCL.Multiplayer.Connections.Messaging;
using Decentraland.Kernel.Comms.Rfc4;
using ECS;
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
    public class CommunicationsControllerAPIImplementationBase : ICommunicationsControllerAPI
    {
        internal enum MsgType
        {
            String = 1, // SDK scenes MessageBus messages
            Uint8Array = 2,
        }

        protected readonly CancellationTokenSource cancellationTokenSource = new ();
        protected readonly ICommunicationControllerHub messagePipesHub;
        protected readonly ISceneData sceneData;
        protected readonly ISceneStateProvider sceneStateProvider;
        protected readonly IJsOperations jsOperations;
        protected readonly Action<ICommunicationControllerHub.SceneMessage> onMessageReceivedCached;
        protected readonly List<IMemoryOwner<byte>> eventsToProcess = new ();
        private readonly IRealmData realmData;
        internal IReadOnlyList<IMemoryOwner<byte>> EventsToProcess => eventsToProcess;

        public CommunicationsControllerAPIImplementationBase(
            IRealmData realmData,
            ISceneData sceneData,
            ICommunicationControllerHub messagePipesHub,
            IJsOperations jsOperations,
            ISceneStateProvider sceneStateProvider)
        {
            this.realmData = realmData;
            this.sceneData = sceneData;
            this.messagePipesHub = messagePipesHub;
            this.jsOperations = jsOperations;
            this.sceneStateProvider = sceneStateProvider;

            onMessageReceivedCached = OnMessageReceived;

            // if it's the world subscribe to the messages straight-away
            if (IgnoreIsCurrentScene())
                this.messagePipesHub.SetSceneMessageHandler(onMessageReceivedCached);
        }

        public void Dispose()
        {
            messagePipesHub.RemoveSceneMessageHandler(onMessageReceivedCached);

            lock (eventsToProcess) { CleanUpReceivedMessages(); }

            cancellationTokenSource.SafeCancelAndDispose();
        }

        public void OnSceneIsCurrentChanged(bool isCurrent)
        {
            if (IgnoreIsCurrentScene()) return;

            if (isCurrent)
                messagePipesHub.SetSceneMessageHandler(onMessageReceivedCached);
            else
                messagePipesHub.RemoveSceneMessageHandler(onMessageReceivedCached);
        }

        public object SendBinary(IReadOnlyList<PoolableByteArray> data)
        {
            if (!IgnoreIsCurrentScene() && !sceneStateProvider.IsCurrent)
                return jsOperations.ConvertToScriptTypedArrays(Array.Empty<IMemoryOwner<byte>>());

            foreach (PoolableByteArray poolable in data)
            {
                if (poolable.Length == 0)
                    continue;

                Memory<byte> message = poolable.Memory;

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

        /// <summary>
        ///     If it's a world there is a single scene for the whole world, it's controlled by the endpoint
        ///     so there is no static reliable way to check it if in the future that behavior changes
        /// </summary>
        /// <returns></returns>
        private bool IgnoreIsCurrentScene() =>
            realmData.ScenesAreFixed;

        private void CleanUpReceivedMessages()
        {
            foreach (IMemoryOwner<byte>? message in eventsToProcess)
                message.Dispose();

            eventsToProcess.Clear();
        }

        private void SendMessage(ReadOnlySpan<byte> message)
        {
            messagePipesHub.SendMessage(message, sceneData.SceneEntityDefinition.id!, cancellationTokenSource.Token);
        }

        protected virtual void OnMessageReceived(ICommunicationControllerHub.SceneMessage receivedMessage) { }

        internal static MsgType DecodeMessage(ref ReadOnlySpan<byte> value)
        {
            var msgType = (MsgType)value[0];
            value = value[1..];
            return msgType;
        }
    }
}
