using CrdtEcsBridge.PoolsProviders;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8.SplitProxy;
using SceneRunner.Scene.ExceptionsHandling;
using System;
using System.Collections.Generic;
using System.Threading;

namespace SceneRuntime.Apis.Modules.CommunicationsControllerApi
{
    public class CommunicationsControllerAPIWrapper : JsApiWrapper<ICommunicationsControllerAPI>, IV8HostObject
    {
        private readonly IInstancePoolsProvider instancePoolsProvider;
        private readonly List<PoolableByteArray> lastInput = new (10);
        private readonly InvokeHostObject sendBinary;

        private readonly ISceneExceptionsHandler sceneExceptionsHandler;

        public CommunicationsControllerAPIWrapper(ICommunicationsControllerAPI api, IInstancePoolsProvider instancePoolsProvider, ISceneExceptionsHandler sceneExceptionsHandler, CancellationTokenSource disposeCts) : base(api, disposeCts)
        {
            this.instancePoolsProvider = instancePoolsProvider;
            this.sceneExceptionsHandler = sceneExceptionsHandler;
            sendBinary = SendBinary;
        }

        protected override void DisposeInternal()
        {
            // Release the last input buffer
            for (var i = 0; i < lastInput.Count; i++)
            {
                PoolableByteArray message = lastInput[i];
                message.ReleaseAndDispose();
            }

            lastInput.Clear();
        }

        private void SendBinaryToParticipants(V8Object dataList, int dataCount, string recipient)
        {
            try
            {
                using var value = V8Value.New();

                for (var i = 0; i < dataCount; i++)
                {
                    dataList.GetIndexedProperty(i, value);
                    using var messageHolder = value.Decode();
                    Uint8Array message = messageHolder.GetUint8Array();

                    PoolableByteArray element = PoolableByteArray.EMPTY;

                    if (lastInput.Count <= i)
                    {
                        instancePoolsProvider.RenewCrdtRawDataPoolFromScriptArray(message, ref element);
                        lastInput.Add(element);
                    }
                    else
                    {
                        element = lastInput[i];
                        instancePoolsProvider.RenewCrdtRawDataPoolFromScriptArray(message, ref element);
                        lastInput[i] = element;
                    }
                }

                // Remove excess elements
                while (lastInput.Count > dataCount)
                {
                    int lastIndex = lastInput.Count - 1;
                    PoolableByteArray message = lastInput[lastIndex];
                    message.ReleaseAndDispose();
                    lastInput.RemoveAt(lastIndex);
                }

                api.SendBinary(lastInput, recipient);
            }
            catch (Exception e)
            {
                sceneExceptionsHandler.OnEngineException(e);
            }
        }

        private void SendBinary(ReadOnlySpan<V8Value.Decoded> args, V8Value result)
        {
            var broadcastData = args[0].GetV8Object();

            var peerData = args.Length > 0 && args[1].Type != V8Value.Type.Null
                ? args[1].GetV8Object() : default;

            result.SetV8Object(SendBinary(broadcastData, peerData));
        }

        private ScriptObject SendBinary(V8Object broadcastData, V8Object peerData)
        {
            using var value = V8Value.New();

            broadcastData.GetNamedProperty("length", value);
            SendBinaryToParticipants(broadcastData, (int)value.GetNumber(), null);

            if (peerData.GetHashCode() != 0)
            {
                peerData.GetNamedProperty("length", value);
                int peerDataCount = (int)value.GetNumber();

                for (var i = 0; i < peerDataCount; i++)
                {
                    peerData.GetIndexedProperty(i, value);
                    using var perRecipientStructHolder = value.Decode();
                    V8Object perRecipientStruct = perRecipientStructHolder.GetV8Object();

                    perRecipientStruct.GetNamedProperty("data", value);
                    using var dataHolder = value.Decode();
                    V8Object data = dataHolder.GetV8Object();

                    data.GetNamedProperty("length", value);
                    int dataCount = (int)value.GetNumber();

                    if (dataCount == 0)
                        continue;

                    perRecipientStruct.GetNamedProperty("address", value);
                    using var recipientHolder = value.Decode();
                    V8Object recipient = recipientHolder.GetV8Object();

                    recipient.GetNamedProperty("length", value);
                    int recipientCount = (int)value.GetNumber();

                    if (recipientCount == 0)
                        SendBinaryToParticipants(data, dataCount, null);
                    else
                        for (int j = 0; j < recipientCount; j++)
                        {
                            recipient.GetIndexedProperty(j, value);
                            string address = value.GetString();

                            if (!string.IsNullOrEmpty(address))
                                SendBinaryToParticipants(data, dataCount, address);
                        }
                }
            }

            return api.GetResult();
        }

        void IV8HostObject.GetNamedProperty(StdString name, V8Value value, out bool isConst)
        {
            isConst = true;

            if (name.Equals(nameof(SendBinary)))
                value.SetHostObject(sendBinary);
            else
                throw new NotImplementedException(
                    $"Named property {name.ToString()} is not implemented");
        }
    }
}
