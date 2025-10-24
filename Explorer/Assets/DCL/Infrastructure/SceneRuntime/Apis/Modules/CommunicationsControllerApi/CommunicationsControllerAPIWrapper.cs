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
        private readonly IInstancePoolsProvider instancePoolsProvider;
        private readonly List<PoolableByteArray> lastInput = new (10);
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
                        var broadcastData = args.Get<IList<object>>(0);
                        var peerData = args.Count > 1 ? args.Get<IList<object>>(1) : null;
                        result.Set(self.SendBinary(broadcastData, peerData));
                    });
            });
        }

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
                for (int i = 0; i < dataList.Count; i++)
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

        /// <param name="broadcastData">Uint8Array[]</param>
        /// <param name="peerData">PeerMessageData[]</param>
        /// <returns>Uint8Array[]</returns>
        private ScriptObject SendBinary(IList<object> broadcastData, IList<object>? peerData)
        {
            SendBinaryToParticipants(broadcastData, null);

            if (peerData != null)
            {
                foreach (ScriptObject peerMessageData in peerData)
                {
                    var data = (IList<object>)peerMessageData.GetProperty("data");

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
