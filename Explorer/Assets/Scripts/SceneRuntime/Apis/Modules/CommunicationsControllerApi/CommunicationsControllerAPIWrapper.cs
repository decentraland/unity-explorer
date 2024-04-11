using CrdtEcsBridge.PoolsProviders;
using DCL.Diagnostics;
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

        private readonly List<PoolableByteArray> lastInput = new (10);

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
                PoolableByteArray message = lastInput[i];
                message.ReleaseAndDispose();
            }

            lastInput.Clear();
        }

        public void OnSceneIsCurrentChanged(bool isCurrent)
        {
            api.OnSceneIsCurrentChanged(isCurrent);
        }

        [UsedImplicitly]
        public object SendBinary(IList<object> dataList)
        {
            try
            {
                for (var i = 0; i < dataList.Count; i++)
                {
                    var message = (ITypedArray<byte>)dataList[i];
                    var messageLength = message.Length;
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

                return api.SendBinary(lastInput);
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.ENGINE);
                throw;
            }
        }
    }
}
