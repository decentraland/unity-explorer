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

        public CommunicationsControllerAPIWrapper(ICommunicationsControllerAPI api, ISceneExceptionsHandler sceneExceptionsHandler, CancellationTokenSource disposeCts) : base(api, disposeCts)
        {
            this.sceneExceptionsHandler = sceneExceptionsHandler;
        }

        private void SendBinaryToParticipants(IList<ITypedArray<byte>> dataList, string? recipient)
        {
            try
            {
                api.SendBinary(dataList, recipient);
            }
            catch (Exception e)
            {
                sceneExceptionsHandler.OnEngineException(e);
            }
        }

        [UsedImplicitly]
        public object SendBinary(IList<ITypedArray<byte>> broadcastData) =>
            SendBinary(broadcastData, null);

        [UsedImplicitly]
        public object SendBinary(IList<ITypedArray<byte>> broadcastData, IList<object>? peerData)
        {
            SendBinaryToParticipants(broadcastData, null);

            if (peerData != null)
                for (var i = 0; i < peerData.Count; i++)
                {
                    object? obj = peerData[i];

                    if (obj is IScriptObject perRecipientStruct)
                    {
                        var recipient = (IList<object>)perRecipientStruct.GetProperty("address")!;
                        var data = (IList<ITypedArray<byte>>)perRecipientStruct.GetProperty("data")!;

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
