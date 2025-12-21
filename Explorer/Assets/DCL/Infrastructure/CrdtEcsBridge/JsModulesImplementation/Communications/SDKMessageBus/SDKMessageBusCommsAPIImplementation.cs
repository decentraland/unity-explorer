using SceneRunner.Scene;
using SceneRuntime;
using SceneRuntime.Apis.Modules.CommunicationsControllerApi.SDKMessageBus;
using SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents.Events;
using System;
using System.Collections.Generic;
using System.Text;

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
            // TODO it can be implemented in alloc free manner
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);

            int length = EncodedMessage.LengthWithReservedByte(dataBytes.Length);
            // TODO Actually it's really NOT safe because the implementation deals with strings.
            Span<byte> contentAlloc = stackalloc byte[length];
            EncodedMessage encodedMessage = new EncodedMessage(contentAlloc);
            encodedMessage.AssignType(ISceneCommunicationPipe.MsgType.String);
            // TODO Should avoid copy
            dataBytes.AsSpan().CopyTo(encodedMessage.Content());

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
