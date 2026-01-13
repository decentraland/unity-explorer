using CrdtEcsBridge.PoolsProviders;
using JetBrains.Annotations;
using SceneRunner.Scene.ExceptionsHandling;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;

#if !UNITY_WEBGL
using SceneRuntime.V8;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
#endif

namespace SceneRuntime.Apis.Modules.CommunicationsControllerApi
{
    public class CommunicationsControllerAPIWrapper : JsApiWrapper<ICommunicationsControllerAPI>
    {
        private readonly IInstancePoolsProvider instancePoolsProvider;

        private readonly List<PoolableByteArray> lastInput = new (10);

        private readonly ISceneExceptionsHandler sceneExceptionsHandler;

        public CommunicationsControllerAPIWrapper(ICommunicationsControllerAPI api, IInstancePoolsProvider instancePoolsProvider, ISceneExceptionsHandler sceneExceptionsHandler, CancellationTokenSource disposeCts) : base(api, disposeCts)
        {
            this.instancePoolsProvider = instancePoolsProvider;
            this.sceneExceptionsHandler = sceneExceptionsHandler;
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

        private void SendBinaryToParticipants(IList<object> dataList, string? recipient)
        {
            try
            {
                for (var i = 0; i < dataList.Count; i++)
                {
                    IDCLTypedArray<byte> dclTypedArray;
                    object dataItem = dataList[i];

                    if (dataItem is IDCLTypedArray<byte> alreadyDcl) dclTypedArray = alreadyDcl;
#if UNITY_WEBGL
                    else throw new InvalidCastException($"WebGL needs IDCLTypedArray<byte> but got {dataItem?.GetType()}");
#else
                    else if (dataItem is ITypedArray<byte> typedArray)
                        dclTypedArray = new V8TypedArrayAdapter(typedArray);
                    else throw new InvalidCastException($"Expected ITypedArray<byte> or IDCLTypedArray<byte>, but got {dataItem?.GetType()}");
#endif
                    PoolableByteArray element = PoolableByteArray.EMPTY;

                    if (lastInput.Count <= i)
                    {
                        instancePoolsProvider.RenewCrdtRawDataPoolFromScriptArray(dclTypedArray, ref element);
                        lastInput.Add(element);
                    }
                    else
                    {
                        element = lastInput[i];
                        instancePoolsProvider.RenewCrdtRawDataPoolFromScriptArray(dclTypedArray, ref element);
                        lastInput[i] = element;
                    }
                }

                // Remove excess elements
                while (lastInput.Count > dataList.Count)
                {
                    int lastIndex = lastInput.Count - 1;
                    PoolableByteArray message = lastInput[lastIndex];
                    message.ReleaseAndDispose();
                    lastInput.RemoveAt(lastIndex);
                }

                api.SendBinary(lastInput, recipient);
            }
            catch (Exception e) { sceneExceptionsHandler.OnEngineException(e); }
        }

        [UsedImplicitly]
        public object SendBinary(IList<object> broadcastData) =>
            SendBinary(broadcastData, null);

        [UsedImplicitly]
        public object SendBinary(IList<object> broadcastData, IList<object>? peerData)
        {
            SendBinaryToParticipants(broadcastData, null);

            if (peerData != null)
                for (var i = 0; i < peerData.Count; i++)
                {
                    object? obj = peerData[i];

                    if (obj is IDCLScriptObject perRecipientStruct)
                    {
                        var recipient = (IList<object>)perRecipientStruct.GetProperty("address");
                        var data = (IList<object>)perRecipientStruct.GetProperty("data");

                        if (data.Count is 0)
                            continue;

                        if (recipient.Count is 0)
                            SendBinaryToParticipants(data, null);

                        foreach (object? address in recipient)
                            if (address != null)
                            {
                                var stringAddress = (string)address;

                                if (!string.IsNullOrEmpty(stringAddress))
                                    SendBinaryToParticipants(data, stringAddress);
                            }
                    }
                }

            IDCLScriptObject result = api.GetResult();

            // Get the native JavaScript object for proper enumeration support
            // ClearScript can handle ScriptObject natively but not adapter wrappers
            // When passed to JavaScript, ScriptObject is enumerable but V8ScriptObjectAdapter is not
            object nativeObject = result.GetNativeObject();
#if !UNITY_WEBGL
            // For V8, ensure we return ScriptObject for proper JavaScript enumeration
            if (nativeObject is ScriptObject so)
                return so;
#endif
            return nativeObject;
        }
    }
}
