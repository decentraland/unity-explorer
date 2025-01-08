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
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);
            EncodeAndSendMessage(ISceneCommunicationPipe.MsgType.String, dataBytes, ISceneCommunicationPipe.ConnectivityAssertiveness.DROP_IF_NOT_CONNECTED);
        }

        protected override void OnMessageReceived(ISceneCommunicationPipe.DecodedMessage message)
        {
            messages.Add(new CommsPayload
            {
                sender = message.FromWalletId,
                message = Encoding.UTF8.GetString(message.Data),
            });
        }
    }
}
