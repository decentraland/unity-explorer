using System;
using System.IO;
using System.Threading.Tasks;
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
        return api.CrdtSendToRenderer(data);
    }
    
    [UsedImplicitly]
    public object CrdtGetState()
    {
        return api.CrdtGetState();
    }
}