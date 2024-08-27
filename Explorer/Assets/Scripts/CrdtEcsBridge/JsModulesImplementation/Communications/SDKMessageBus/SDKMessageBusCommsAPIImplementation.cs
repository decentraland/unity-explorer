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

        public SDKMessageBusCommsAPIImplementation(ISceneData sceneData, ISceneCommunicationPipe sceneCommunicationPipe, IJsOperations jsOperations, ISceneStateProvider sceneStateProvider) : base(sceneData, sceneCommunicationPipe, jsOperations, sceneStateProvider) { }

        public void ClearMessages()
        {
            messages.Clear();
        }

        public void Send(string data)
        {
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);
            EncodeAndSendMessage(MsgType.String, dataBytes);
        }

        protected override void OnMessageReceived(MsgType messageType, ReadOnlySpan<byte> decodedMessage, string fromWalletId)
        {
            if (messageType != MsgType.String)
                return;

            messages.Add(new CommsPayload
            {
                sender = fromWalletId,
                message = Encoding.UTF8.GetString(decodedMessage)
            });
        }
    }
}
