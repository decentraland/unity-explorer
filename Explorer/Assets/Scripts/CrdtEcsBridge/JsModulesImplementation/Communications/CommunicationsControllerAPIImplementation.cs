using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using Decentraland.Kernel.Comms.Rfc4;
using Google.Protobuf;
using JetBrains.Annotations;
using SceneRunner.Scene;
using SceneRuntime;
using SceneRuntime.Apis.Modules.CommunicationsControllerApi;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace CrdtEcsBridge.CommunicationsController
{
    public class CommunicationsControllerAPIImplementation : ICommunicationsControllerAPI
    {
        private readonly CancellationTokenSource cancellationTokenSource = new ();
        private readonly List<byte[]> eventsToProcess = new ();
        private readonly IJsOperations jsOperations;
        private readonly ICommunicationControllerHub messagePipesHub;
        private readonly ISceneData sceneData;

        public CommunicationsControllerAPIImplementation(
            ISceneData sceneData,
            ICommunicationControllerHub messagePipesHub,
            IJsOperations jsOperations)
        {
            this.sceneData = sceneData;
            this.messagePipesHub = messagePipesHub;
            this.jsOperations = jsOperations;
        }

        public void OnSceneBecameCurrent()
        {
            messagePipesHub.SetSceneMessageHandler(OnMessageReceived);
        }

        public object SendBinary(byte[][] data)
        {
            foreach (byte[] message in data)
            {
                if (message.Length == 0)
                    continue;

                byte[] encodedMessage = EncodeMessage(message, MsgType.Uint8Array);
                SendMessage(encodedMessage);
            }

            byte[][] resultData = eventsToProcess.ToArray();
            eventsToProcess.Clear();
            return jsOperations.ConvertToScriptTypedArrays(resultData);
        }

        public void Dispose()
        {
            // TODO clean-up events to process

            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }

        private void SendMessage(ReadOnlySpan<byte> message)
        {
            messagePipesHub.SendMessage(message, sceneData.SceneEntityDefinition.id, cancellationTokenSource.Token);
        }

        private void OnMessageReceived(ReceivedMessage<Scene> receivedMessage)
        {
            (MsgType msgType, byte[] data) decodedMessage = DecodeMessage(receivedMessage.Payload.Data.ToByteArray());

            if (decodedMessage.msgType != MsgType.Uint8Array || decodedMessage.data.Length == 0)
                return;

            byte[] senderBytes = Encoding.UTF8.GetBytes(receivedMessage.FromWalletId);
            int messageLength = senderBytes.Length + decodedMessage.data.Length + 1;
            var serializedMessage = new byte[messageLength];
            serializedMessage[0] = (byte)senderBytes.Length;
            Array.Copy(senderBytes, 0, serializedMessage, 1, senderBytes.Length);
            Array.Copy(decodedMessage.data, 0, serializedMessage, senderBytes.Length + 1, decodedMessage.data.Length);

            eventsToProcess.Add(serializedMessage);
        }

        private static byte[] EncodeMessage(byte[] data, MsgType type)
        {
            var message = new byte[data.Length + 1];
            message[0] = (byte)type;
            Array.Copy(data, 0, message, 1, data.Length);
            return message;
        }

        private static (MsgType msgType, byte[] data) DecodeMessage(byte[] value)
        {
            var msgType = (MsgType)value[0];
            var data = new byte[value.Length - 1];
            Array.Copy(value, 1, data, 0, value.Length - 1);
            return (msgType, data);
        }

        private enum MsgType
        {
            String = 1, // Deprecated in SDK7
            Uint8Array = 2,
        }
    }
}
