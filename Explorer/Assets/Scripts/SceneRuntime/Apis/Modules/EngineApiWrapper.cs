using System;
using System.IO;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8;

public class EngineApiWrapper
{
    private readonly IEngineApi api;
    public EngineApiWrapper(IEngineApi api)
    {
        this.api = api;
    }
    
    [UsedImplicitly]
    public object CrdtSendToRenderer(ITypedArray<byte> data)
    {
        // TODO: Implement ToPromise to UniTask
        return api.CrdtSendToRenderer(data).AsTask().ToPromise();
    }
    
    [UsedImplicitly]
    public object CrdtGetState()
    {
        return api.CrdtGetState().AsTask().ToPromise();
    }
}