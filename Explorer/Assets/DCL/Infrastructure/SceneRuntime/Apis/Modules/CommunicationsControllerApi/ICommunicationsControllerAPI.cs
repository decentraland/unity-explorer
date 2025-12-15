using CrdtEcsBridge.PoolsProviders;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8.FastProxy;
using System;
using System.Collections.Generic;

namespace SceneRuntime.Apis.Modules.CommunicationsControllerApi
{
    public interface ICommunicationsControllerAPI : IDisposable
    {
        void SendBinary(IEnumerable<ITypedArray<byte>> broadcastData, string? recipient = null);

        ScriptObject GetResult();
    }
}
