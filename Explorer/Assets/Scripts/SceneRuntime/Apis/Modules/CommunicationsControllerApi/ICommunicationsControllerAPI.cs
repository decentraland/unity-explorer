using CrdtEcsBridge.PoolsProviders;
using System;
using System.Collections.Generic;

namespace SceneRuntime.Apis.Modules.CommunicationsControllerApi
{
    public interface ICommunicationsControllerAPI : IDisposable
    {
        void SendBinary(IReadOnlyList<PoolableByteArray> broadcastData, string? recipient = null);

        object GetResult();
    }
}
