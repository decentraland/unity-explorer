using CrdtEcsBridge.PoolsProviders;
using JetBrains.Annotations;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using SceneRunner.Scene.ExceptionsHandling;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

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
                    var message = (ITypedArray<byte>)dataList[i];
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
                while (lastInput.Count > dataList.Count)
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
                        var recipient = (IList<object>)perRecipientStruct.GetProperty("address")!;
                        var data = (IList<object>)perRecipientStruct.GetProperty("data")!;

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


            //TODO FRAN: FIX THIS!! Cant use reflection and all these hacks
            IDCLScriptObject result = api.GetResult();
            // Convert V8ScriptObjectAdapter to ScriptObject for JavaScript enumeration support
            // ClearScript can handle ScriptObject natively but not adapter wrappers
            // When passed to JavaScript, ScriptObject is enumerable but V8ScriptObjectAdapter is not
            // Use reflection to avoid circular assembly reference
#if UNITY_WEBGL
            if (result != null)
            {
                Type resultType = result.GetType();
                // Check if it's a V8ScriptObjectAdapter without directly referencing the type
                if (resultType.Name == "V8ScriptObjectAdapter" && resultType.Namespace == "SceneRuntime.V8")
                {
                    // Get the ScriptObject property via reflection
                    PropertyInfo? scriptObjectProperty = resultType.GetProperty("ScriptObject", BindingFlags.Public | BindingFlags.Instance);
                    if (scriptObjectProperty != null)
                    {
                        object? scriptObject = scriptObjectProperty.GetValue(result);
                        if (scriptObject is ScriptObject so)
                            return so;
                    }
                }
                // Check if it's a V8TypedArrayAdapter and unwrap it
                if (resultType.Name == "V8TypedArrayAdapter" && resultType.Namespace == "SceneRuntime.V8")
                {
                    PropertyInfo? scriptObjectProperty = resultType.GetProperty("ScriptObject", BindingFlags.Public | BindingFlags.Instance);
                    if (scriptObjectProperty != null)
                    {
                        object? scriptObject = scriptObjectProperty.GetValue(result);
                        if (scriptObject is ScriptObject so)
                            return so;
                    }
                }
            }
#endif
            return result;
        }
    }
}
