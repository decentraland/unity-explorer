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

        public SDKMessageBusCommsAPIImplementation(ISceneData sceneData, ICommunicationControllerHub communicationControllerHub, IJsOperations jsOperations, ISceneStateProvider sceneStateProvider) : base(sceneData, communicationControllerHub, jsOperations, sceneStateProvider) { }

        public void ClearMessages()
        {
            messages.Clear();
        }

        public void Send(string data)
        {
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);
            Span<byte> encodedMessage = stackalloc byte[dataBytes.Length + 1];
            encodedMessage[0] = (byte)MsgType.String;
            dataBytes.CopyTo(encodedMessage[1..]);

            communicationControllerHub.SendMessage(encodedMessage, sceneData.SceneEntityDefinition.id, cancellationTokenSource.Token);
        }

        protected override void OnMessageReceived(ICommunicationControllerHub.SceneMessage receivedMessage)
        {
            ReadOnlySpan<byte> decodedMessage = receivedMessage.Data.Span;
            MsgType msgType = DecodeMessage(ref decodedMessage);

            if (msgType != MsgType.String || decodedMessage.Length == 0)
                return;

            messages.Add(new CommsPayload
            {
                sender = receivedMessage.FromWalletId,
                message = Encoding.UTF8.GetString(decodedMessage)
            });
        }
    }
}
