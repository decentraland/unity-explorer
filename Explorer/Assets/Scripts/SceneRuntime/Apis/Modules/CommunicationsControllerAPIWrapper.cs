using JetBrains.Annotations;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Collections.Generic;

namespace SceneRuntime.Apis.Modules
{
    public class CommunicationsControllerAPIWrapper : IDisposable
    {
        private readonly ICommunicationsControllerAPI api;

        public CommunicationsControllerAPIWrapper(ICommunicationsControllerAPI api)
        {
            this.api = api;
        }

        public void Dispose()
        {
            api.Dispose();
        }

        [UsedImplicitly]
        public object SendBinary(IList<object> dataList)
        {
            List<byte[]> data = new List<byte[]>();
            foreach (ITypedArray<byte> message in dataList)
                data.Add(message.ToArray());

            return api.SendBinary(data.ToArray());
        }
    }
}
