using CrdtEcsBridge.PoolsProviders;
using DCL.Diagnostics;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8.SplitProxy;
using System;
using System.Collections.Generic;

namespace SceneRuntime.Apis.Modules.CommunicationsControllerApi
{
    public class CommunicationsControllerAPIWrapper : JsApiWrapperBase<ICommunicationsControllerAPI>,
        IV8HostObject
    {
        private readonly IInstancePoolsProvider instancePoolsProvider;
        private readonly List<PoolableByteArray> lastInput = new (10);
        private readonly InvokeHostObject sendBinary;

        public CommunicationsControllerAPIWrapper(ICommunicationsControllerAPI api, IInstancePoolsProvider instancePoolsProvider) : base(api)
        {
            this.instancePoolsProvider = instancePoolsProvider;
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
                for (var i = 0; i < dataCount; i++)
                {
                    Uint8Array message;

                    using (var value = V8Value.New())
                    {
                        dataList.GetIndexedProperty(i, value);
                        var decoded = value.Decode();
                        message = decoded.GetUint8Array();
                    }

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
                ReportHub.LogException(e, ReportCategory.ENGINE);
                throw;
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
            using (var value = V8Value.New())
            {
                broadcastData.GetNamedProperty("length", value);
                var decoded = value.Decode();
                SendBinaryToParticipants(broadcastData, (int)decoded.GetNumber(), null);
            }

            if (peerData.GetHashCode() != 0)
            {
                int peerDataCount;

                using (var value = V8Value.New())
                {
                    peerData.GetNamedProperty("length", value);
                    var decoded = value.Decode();
                    peerDataCount = (int)decoded.GetNumber();
                }

                for (var i = 0; i < peerDataCount; i++)
                {
                    V8Object perRecipientStruct;

                    using (var value = V8Value.New())
                    {
                        peerData.GetIndexedProperty(i, value);
                        var decoded = value.Decode();
                        perRecipientStruct = decoded.GetV8Object();
                    }

                    V8Object data;

                    using (var value = V8Value.New())
                    {
                        perRecipientStruct.GetNamedProperty("data", value);
                        var decoded = value.Decode();
                        data = decoded.GetV8Object();
                    }

                    int dataCount;

                    using (var value = V8Value.New())
                    {
                        data.GetNamedProperty("length", value);
                        var decoded = value.Decode();
                        dataCount = (int)decoded.GetNumber();
                    }

                    if (dataCount == 0)
                        continue;

                    V8Object recipient;

                    using (var value = V8Value.New())
                    {
                        perRecipientStruct.GetNamedProperty("address", value);
                        var decoded = value.Decode();
                        recipient = decoded.GetV8Object();
                    }

                    int recipientCount;

                    using (var value = V8Value.New())
                    {
                        recipient.GetNamedProperty("length", value);
                        var decoded = value.Decode();
                        recipientCount = (int)decoded.GetNumber();
                    }

                    if (recipientCount == 0)
                        SendBinaryToParticipants(data, dataCount, null);
                    else
                        for (int j = 0; j < recipientCount; j++)
                        {
                            string address;

                            using (var value = V8Value.New())
                            {
                                recipient.GetIndexedProperty(j, value);
                                var decoded = value.Decode();
                                address = decoded.GetString();
                            }

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
