using DCL.Multiplayer.Connections.Messaging;
using Decentraland.Kernel.Comms.Rfc4;
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
        public List<CommsPayload> SceneCommsMessages { get; } = new List<CommsPayload>();

        public SDKMessageBusCommsAPIImplementation(ISceneData sceneData, ICommunicationControllerHub messagePipesHub, IJsOperations jsOperations, ISceneStateProvider sceneStateProvider) : base(sceneData, messagePipesHub, jsOperations, sceneStateProvider)
        {
        }

        public void Send(string data)
        {
            var dataBytes = Encoding.UTF8.GetBytes(data);
            Span<byte> encodedMessage = stackalloc byte[dataBytes.Length + 1];
            encodedMessage[0] = (byte)MsgType.String;
            dataBytes.CopyTo(encodedMessage[1..]);

            messagePipesHub.SendMessage(encodedMessage, sceneData.SceneEntityDefinition.id, cancellationTokenSource.Token);
        }

        protected override void OnMessageReceived(ReceivedMessage<Scene> receivedMessage)
        {
            using (receivedMessage)
            {
                ReadOnlySpan<byte> decodedMessage = receivedMessage.Payload.Data.Span;
                MsgType msgType = DecodeMessage(ref decodedMessage);

                if (msgType != MsgType.String || decodedMessage.Length == 0)
                    return;

                SceneCommsMessages.Add(new CommsPayload()
                {
                    sender = receivedMessage.FromWalletId,
                    message = Encoding.UTF8.GetString(decodedMessage)
                });
            }
        }
    }
}
