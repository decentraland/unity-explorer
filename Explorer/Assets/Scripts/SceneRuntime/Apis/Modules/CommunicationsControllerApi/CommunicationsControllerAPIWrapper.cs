using CrdtEcsBridge.PoolsProviders;
using JetBrains.Annotations;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Collections.Generic;

namespace SceneRuntime.Apis.Modules.CommunicationsControllerApi
{
    public class CommunicationsControllerAPIWrapper : IJsApiWrapper
    {
        private readonly ICommunicationsControllerAPI api;
        private readonly IInstancePoolsProvider instancePoolsProvider;

        private readonly List<byte[]> lastInput = new (10);

        public CommunicationsControllerAPIWrapper(ICommunicationsControllerAPI api, IInstancePoolsProvider instancePoolsProvider)
        {
            this.api = api;
            this.instancePoolsProvider = instancePoolsProvider;
        }

        public void Dispose()
        {
            api.Dispose();

            // Release the last input buffer
            for (var i = 0; i < lastInput.Count; i++)
            {
                byte[] message = lastInput[i];
                instancePoolsProvider.ReleaseAndDispose(ref message);
            }
        }

        public void OnSceneBecameCurrent()
        {
            api.OnSceneBecameCurrent();
        }

        [UsedImplicitly]
        public object SendBinary(IList<object> dataList)
        {
            for (var i = 0; i < dataList.Count; i++)
            {
                var message = (ITypedArray<byte>)dataList[i];
                byte[]? element = null;

                if (lastInput.Count <= i)
                {
                    instancePoolsProvider.RenewCrdtRawDataPoolFromScriptArray(message, ref element);
                    lastInput.Add(element);
                }
                else
                {
                    element = lastInput[i];
                    instancePoolsProvider.RenewCrdtRawDataPoolFromScriptArray(message, ref element);
                }
            }

            // Remove excess elements
            while (lastInput.Count > dataList.Count)
            {
                int lastIndex = lastInput.Count - 1;
                byte[] message = lastInput[lastIndex];
                instancePoolsProvider.ReleaseAndDispose(ref message);
                lastInput.RemoveAt(lastIndex);
            }

            return api.SendBinary(lastInput);
        }
    }
}
