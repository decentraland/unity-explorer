using SceneRunner.Scene;
using SceneRuntime;
using SceneRuntime.Apis.Modules.CommunicationsControllerApi.SDKMessageBus;
using SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents.Events;
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;

namespace CrdtEcsBridge.JsModulesImplementation.Communications.SDKMessageBus
{
    public class SDKMessageBusCommsAPIImplementation : CommunicationsControllerAPIImplementationBase, ISDKMessageBusCommsControllerAPI
    {
        private readonly List<CommsPayload> messages = new ();

        public IReadOnlyList<CommsPayload> SceneCommsMessages => messages;

        public SDKMessageBusCommsAPIImplementation(ISceneData sceneData, ISceneCommunicationPipe sceneCommunicationPipe, IJsOperations jsOperations)
            : base(sceneData, sceneCommunicationPipe, jsOperations, ISceneCommunicationPipe.MsgType.String) { }

        public void ClearMessages()
        {
            messages.Clear();
        }

        public void Send(string data)
        {
            int byteCount = Encoding.UTF8.GetByteCount(data);
            int length = EncodedMessage.LengthWithReservedByte(byteCount);
            using NativeArray<byte> dataBytes = new NativeArray<byte>(length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            EncodedMessage encodedMessage = new EncodedMessage(dataBytes.AsSpan());

            Span<byte> contentSpan = encodedMessage.Content();
            Encoding.UTF8.GetBytes(data, contentSpan);

            encodedMessage.AssignType(ISceneCommunicationPipe.MsgType.String);

            EncodeAndSendMessage(encodedMessage, ISceneCommunicationPipe.ConnectivityAssertiveness.DROP_IF_NOT_CONNECTED, null);
        }

        protected override void OnMessageReceived(ISceneCommunicationPipe.DecodedMessage message)
        {
            lock (SceneCommsMessages)
            {
                messages.Add(new CommsPayload
                {
                    sender = message.FromWalletId,
                    message = Encoding.UTF8.GetString(message.Data),
                });
            }
        }
    }
}
