using System;
using System.IO;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8;

public class UnityEngineApi : ISceneApi
{
    private readonly SceneRuntime runtime;
    public UnityEngineApi(SceneRuntime runtime)
    {
        this.runtime = runtime;
        
        runtime.Engine.AddHostObject("UnityEngineApi", this);
    }
    
    [UsedImplicitly]
    public object CrdtSendToRenderer(ITypedArray<byte> data)
    {
        // TODO: Modify this implementation
        return Task.Run(() =>
        {
            return data;
        }).ToPromise();
    }
    
    [UsedImplicitly]
    public object CrdtGetState()
    {
        // prepare .NET byte array
        var bytes = new byte[4096];

        // create script byte array
        var array = (ITypedArray<byte>)runtime.Engine.Evaluate(
            $"(function () {{ return new Uint8Array({bytes.Length}); }}).valueOf()"
            );

        // transfer data to script byte array
        array.Write(bytes, 0, Convert.ToUInt64(bytes.Length), 0);
        
        return Task.Run(() =>
        {
            return array;
        }).ToPromise();
    }
}