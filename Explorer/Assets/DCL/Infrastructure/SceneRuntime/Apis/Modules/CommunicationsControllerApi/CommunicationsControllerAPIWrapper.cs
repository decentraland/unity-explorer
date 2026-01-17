using JetBrains.Annotations;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using SceneRunner.Scene.ExceptionsHandling;
using System;
using System.Collections.Generic;
using System.Threading;

namespace SceneRuntime.Apis.Modules.CommunicationsControllerApi
{
    public class CommunicationsControllerAPIWrapper : JsApiWrapper<ICommunicationsControllerAPI>
    {
        private readonly ISceneExceptionsHandler sceneExceptionsHandler;
        private readonly List<ITypedArray<byte>> lastInput = new ();

        public CommunicationsControllerAPIWrapper(ICommunicationsControllerAPI api, ISceneExceptionsHandler sceneExceptionsHandler, CancellationTokenSource disposeCts) : base(api, disposeCts)
        {
            this.sceneExceptionsHandler = sceneExceptionsHandler;
        }

        protected override void DisposeInternal()
        {
            // clear GC references to remain objects in the list
            lastInput.Clear();
        }

        private void SendBinaryToParticipants(IList<object> dataList, string? recipient)
        {
            try
            {
                for (int i = 0; i < dataList.Count; i++)
                {
                    ITypedArray<byte> item = (ITypedArray<byte>) dataList[i];
                    if (lastInput.Count <= i) lastInput.Add(item);
                    else lastInput[i] = item;
                }

                // Remove excess elements
                int excess = lastInput.Count - dataList.Count;
                // lastInput doesn't own the ITypedArray objects, disposal is not required
                if (excess > 0) lastInput.RemoveRange(dataList.Count, excess);

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

                    if (obj is IScriptObject perRecipientStruct)
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

            return api.GetResult();
        }
    }
}
