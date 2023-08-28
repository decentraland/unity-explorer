using Cysharp.Threading.Tasks;
using Microsoft.ClearScript.JavaScript;
using SceneRuntime.Apis.Modules;
using System;

namespace SceneRuntime
{
    public interface IJSOperations : IDisposable
    {
        ITypedArray<byte> CreateUint8Array(int bytes);
    }
}
