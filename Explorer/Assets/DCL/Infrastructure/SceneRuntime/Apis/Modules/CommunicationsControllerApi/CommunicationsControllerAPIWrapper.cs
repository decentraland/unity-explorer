using CrdtEcsBridge.PoolsProviders;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8.FastProxy;
using SceneRunner.Scene.ExceptionsHandling;
using System;
using System.Collections.Generic;
using System.Threading;

namespace SceneRuntime.Apis.Modules.CommunicationsControllerApi
{
    public sealed class CommunicationsControllerAPIWrapper : JsApiWrapper<ICommunicationsControllerAPI>, IV8FastHostObject
    {
        private static readonly V8FastHostObjectOperations<CommunicationsControllerAPIWrapper> OPERATIONS = new();
        IV8FastHostObjectOperations IV8FastHostObject.Operations => OPERATIONS;
        private readonly ISceneExceptionsHandler sceneExceptionsHandler;

        static CommunicationsControllerAPIWrapper()
        {
            OPERATIONS.Configure(static configuration =>
            {
                configuration.AddMethodGetter(nameof(SendBinary),
                    static (CommunicationsControllerAPIWrapper self, in V8FastArgs args, in V8FastResult result) =>
                    {
                        var broadcastData = args.Get<IList<ITypedArray<byte>>>(0);
                        var peerData = args.Count > 1 ? args.Get<IList<object>>(1) : null;
                        var binaryResult = self.SendBinary(broadcastData, peerData);
                        result.Set(binaryResult);
                    });
            });
        }

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

        /// <param name="broadcastData">Uint8Array[]</param>
        /// <param name="peerData">PeerMessageData[]</param>
        /// <returns>Uint8Array[]</returns>
        private ScriptObject SendBinary(IList<ITypedArray<byte>> broadcastData, IList<object>? peerData)
        {
            SendBinaryToParticipants(broadcastData, null);

            if (peerData != null)
            {
                foreach (ScriptObject peerMessageData in peerData)
                {
                    var data = (IList<ITypedArray<byte>>)peerMessageData.GetProperty("data");

                    if (data.Count == 0)
                        continue;

                    var address = (IList<object>)peerMessageData.GetProperty("address");

                    if (address.Count == 0)
                        SendBinaryToParticipants(data, null);
                    else
                        foreach (string recipient in address)
                            if (!string.IsNullOrEmpty(recipient))
                                SendBinaryToParticipants(data, recipient);
                }
            }

            return api.GetResult();
        }
    }
}
