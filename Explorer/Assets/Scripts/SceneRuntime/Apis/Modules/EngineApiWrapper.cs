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
    public object CrdtSendToRenderer(ITypedArray<byte> data) =>
        api.CrdtSendToRenderer(data.GetBytes()).AsTask().ToPromise();

    [UsedImplicitly]
    public object CrdtGetState() =>
        api.CrdtGetState().AsTask().ToPromise();
}
