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
        // Type constraints to avoid boxing
        void SendBinary<TEnumerable, TArray>(TEnumerable broadcastData, string? recipient = null)
            where TEnumerable : IEnumerable<TArray>
            where TArray : IPoolableByteArray;

        ScriptObject GetResult();
    }
}
